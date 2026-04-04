using Dcs.RabbitMq.Messaging.Messaging;
using ProtoBuf;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.RabbitMq.Messaging.Tcp
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
            var envelope = CreateEnvelope(endpointDetails.Address, message, targetSessionId);
            var payload = SerializeEnvelope(envelope);

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

        private static TcpEnvelope CreateEnvelope(string endpointAddress, IMessage message, string targetSessionId)
        {
            var properties = message.Properties as MessageProperties;
            var propertyValues = new List<TcpPropertyValue>();

            if (properties != null)
            {
                foreach (var pair in properties.ToDictionary())
                {
                    propertyValues.Add(TcpPropertyValue.From(pair.Key, pair.Value));
                }
            }

            return new TcpEnvelope
            {
                EndpointAddress = endpointAddress,
                Payload = message.Payload ?? Array.Empty<byte>(),
                Properties = propertyValues,
                SenderSessionId = message.SenderSessionId,
                Tag = message.Tag,
                TargetSessionId = targetSessionId ?? string.Empty
            };
        }

        private void HandleIncomingEnvelope(TcpConnectionContext connection, TcpEnvelope envelope)
        {
            if (!string.IsNullOrWhiteSpace(envelope.SenderSessionId))
            {
                connection.SetRemoteSessionId(envelope.SenderSessionId);
                _connectionsBySessionId[envelope.SenderSessionId] = connection;
            }

            var properties = new MessageProperties();
            foreach (var property in envelope.Properties ?? Enumerable.Empty<TcpPropertyValue>())
            {
                property.ApplyTo(properties);
            }

            var message = new Message(
                envelope.SenderSessionId ?? string.Empty,
                envelope.Tag ?? string.Empty,
                properties,
                envelope.Payload ?? Array.Empty<byte>());

            _incomingMessages.OnNext(new IncomingTransportMessage(envelope.EndpointAddress, envelope.TargetSessionId, message));
        }

        private async Task ProcessConnectionAsync(TcpConnectionContext connection)
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    var envelope = await ReadEnvelopeAsync(connection.Stream, _shutdown.Token).ConfigureAwait(false);
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

        private static async Task<TcpEnvelope> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
            if (header == null)
            {
                return null;
            }

            var payloadLength = BinaryPrimitives.ReadInt32BigEndian(header);
            if (payloadLength <= 0)
            {
                return null;
            }

            var payload = await ReadExactAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);
            if (payload == null)
            {
                return null;
            }

            using var payloadStream = new MemoryStream(payload, writable: false);
            return Serializer.Deserialize<TcpEnvelope>(payloadStream);
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            var offset = 0;

            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                offset += read;
            }

            return buffer;
        }

        private static byte[] SerializeEnvelope(TcpEnvelope envelope)
        {
            using var payloadStream = new MemoryStream();
            Serializer.Serialize(payloadStream, envelope);
            var payload = payloadStream.ToArray();
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), payload.Length);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            return frame;
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

        private sealed class IncomingTransportMessage
        {
            public IncomingTransportMessage(string endpointAddress, string targetSessionId, IMessage message)
            {
                EndpointAddress = endpointAddress;
                TargetSessionId = targetSessionId;
                Message = message;
            }

            public string EndpointAddress { get; }

            public IMessage Message { get; }

            public string TargetSessionId { get; }
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

        [ProtoContract]
        private sealed class TcpEnvelope
        {
            [ProtoMember(1)]
            public string EndpointAddress { get; set; }

            [ProtoMember(2)]
            public string SenderSessionId { get; set; }

            [ProtoMember(3)]
            public string TargetSessionId { get; set; }

            [ProtoMember(4)]
            public string Tag { get; set; }

            [ProtoMember(5)]
            public List<TcpPropertyValue> Properties { get; set; }

            [ProtoMember(6)]
            public byte[] Payload { get; set; }
        }

        [ProtoContract]
        private sealed class TcpPropertyValue
        {
            [ProtoMember(1)]
            public string BoolValue { get; set; }

            [ProtoMember(2)]
            public string DoubleValue { get; set; }

            [ProtoMember(3)]
            public string IntValue { get; set; }

            [ProtoMember(4)]
            public string Key { get; set; }

            [ProtoMember(5)]
            public string Kind { get; set; }

            [ProtoMember(6)]
            public string StringValue { get; set; }

            public void ApplyTo(MessageProperties properties)
            {
                switch (Kind)
                {
                    case "bool":
                        properties.Set(Key, bool.Parse(BoolValue));
                        break;
                    case "double":
                        properties.Set(Key, double.Parse(DoubleValue, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "int":
                        properties.Set(Key, int.Parse(IntValue, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    default:
                        properties.Set(Key, StringValue);
                        break;
                }
            }

            public static TcpPropertyValue From(string key, object value)
            {
                switch (value)
                {
                    case bool boolValue:
                        return new TcpPropertyValue { BoolValue = boolValue.ToString(), Key = key, Kind = "bool" };
                    case double doubleValue:
                        return new TcpPropertyValue { DoubleValue = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Key = key, Kind = "double" };
                    case int intValue:
                        return new TcpPropertyValue { IntValue = intValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Key = key, Kind = "int" };
                    default:
                        return new TcpPropertyValue { Key = key, Kind = "string", StringValue = Convert.ToString(value) };
                }
            }
        }
    }
}