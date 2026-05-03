using Dcs.Messaging;
using Dcs.Messaging.Transport;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Tcp
{
    public sealed class TcpMessagingSession : IDisposable
    {
        private readonly ISubject<IncomingTransportMessage> _incomingMessages;
        private readonly CancellationTokenSource _shutdown;
        private readonly ConcurrentDictionary<string, TcpConnectionContext> _connectionsBySessionId;
        private readonly ConcurrentDictionary<long, TcpConnectionContext> _connections;
        private readonly TcpSessionOptions _options;
        private TcpListener _listener;
        private long _connectionCounter;

        public TcpMessagingSession(TcpSessionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _incomingMessages = Subject.Synchronize(new Subject<IncomingTransportMessage>());
            _shutdown = new CancellationTokenSource();
            _connectionsBySessionId = new ConcurrentDictionary<string, TcpConnectionContext>(StringComparer.Ordinal);
            _connections = new ConcurrentDictionary<long, TcpConnectionContext>();

            if (_options.Mode == TcpSessionMode.Server)
            {
                StartServer();
            }
            else
            {
                ConnectClient();
            }
        }

        public string SessionId => _options.SessionId;

        public void Dispose()
        {
            if (_shutdown.IsCancellationRequested)
            {
                return;
            }

            _shutdown.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }

            _incomingMessages.OnCompleted();
            _shutdown.Dispose();
        }

        internal IMessageStream GetMessageStream(IEndpointDetails endpointDetails)
        {
            var messages = _incomingMessages
                .Where(incoming => string.Equals(incoming.EndpointAddress, endpointDetails.Address, StringComparison.Ordinal))
                .Where(incoming => string.IsNullOrEmpty(incoming.TargetSessionId) || string.Equals(incoming.TargetSessionId, SessionId, StringComparison.Ordinal))
                .Select(incoming => incoming.Message);

            return new MessageStream(messages);
        }

        internal void Send(IEndpointDetails endpointDetails, IMessage message, string targetSessionId)
        {
            var envelope = TransportEnvelopeFactory.FromMessage(endpointDetails.Address, message, targetSessionId);
            var payload = TransportTcpFraming.SerializeLengthPrefixed(envelope);

            if (_options.Mode == TcpSessionMode.Client)
            {
                var connection = _connections.Values.SingleOrDefault();
                if (connection == null)
                {
                    throw new InvalidOperationException("Client is not connected to a server endpoint.");
                }

                connection.Send(payload);
                return;
            }

            if (!string.IsNullOrEmpty(targetSessionId))
            {
                if (_connectionsBySessionId.TryGetValue(targetSessionId, out var connection))
                {
                    connection.Send(payload);
                }

                return;
            }

            foreach (var connection in _connections.Values)
            {
                connection.Send(payload);
            }
        }

        private void ConnectClient()
        {
            var tcpClient = new TcpClient();
            tcpClient.Connect(_options.Host, _options.Port);
            RegisterConnection(tcpClient);
        }

        private void HandleIncomingEnvelope(TcpConnectionContext connection, TransportEnvelope envelope)
        {
            if (!string.IsNullOrWhiteSpace(envelope.SenderSessionId))
            {
                connection.SetRemoteSessionId(envelope.SenderSessionId);
                _connectionsBySessionId[envelope.SenderSessionId] = connection;
            }

            var message = TransportEnvelopeFactory.ToMessage(envelope);
            _incomingMessages.OnNext(new IncomingTransportMessage(envelope.EndpointAddress, envelope.TargetSessionId, message));
        }

        private async Task ProcessConnectionAsync(TcpConnectionContext connection)
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    var envelope = await TransportTcpFraming.ReadEnvelopeAsync(connection.Stream, _shutdown.Token).ConfigureAwait(false);
                    if (envelope == null)
                    {
                        break;
                    }

                    HandleIncomingEnvelope(connection, envelope);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                RemoveConnection(connection);
                connection.Dispose();
            }
        }

        private void RegisterConnection(TcpClient client)
        {
            var id = Interlocked.Increment(ref _connectionCounter);
            var connection = new TcpConnectionContext(id, client);
            _connections[id] = connection;
            _ = Task.Run(() => ProcessConnectionAsync(connection), _shutdown.Token);
        }

        private void RemoveConnection(TcpConnectionContext connection)
        {
            _connections.TryRemove(connection.Id, out _);
            if (!string.IsNullOrWhiteSpace(connection.RemoteSessionId))
            {
                _connectionsBySessionId.TryRemove(connection.RemoteSessionId, out _);
            }
        }

        private void StartServer()
        {
            var ipAddress = IPAddress.Parse(_options.Host);
            _listener = new TcpListener(ipAddress, _options.Port);
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync, _shutdown.Token);
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
                    RegisterConnection(tcpClient);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private sealed class TcpConnectionContext : IDisposable
        {
            private readonly SemaphoreSlim _sendLock;
            private readonly TcpClient _tcpClient;

            public TcpConnectionContext(long id, TcpClient tcpClient)
            {
                Id = id;
                _tcpClient = tcpClient;
                Stream = tcpClient.GetStream();
                _sendLock = new SemaphoreSlim(1, 1);
            }

            public long Id { get; }

            public string RemoteSessionId { get; private set; }

            public NetworkStream Stream { get; }

            public void Dispose()
            {
                _sendLock.Dispose();
                Stream.Dispose();
                _tcpClient.Dispose();
            }

            public void Send(byte[] frame)
            {
                _sendLock.Wait();
                try
                {
                    Stream.Write(frame, 0, frame.Length);
                    Stream.Flush();
                }
                finally
                {
                    _sendLock.Release();
                }
            }

            public void SetRemoteSessionId(string remoteSessionId)
            {
                RemoteSessionId = remoteSessionId;
            }
        }
    }
}
