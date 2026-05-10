using Dcs.Messaging;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Transport;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dcs.Messaging.Tcp
{
    /// <summary>
    /// TCP transport session with built-in resiliency:
    /// <list type="bullet">
    /// <item>Per-connection bounded outbound queues. Sends never throw on transport failure.</item>
    /// <item>Client mode automatically reconnects with exponential backoff via <see cref="IRetryPolicy"/>.</item>
    /// <item>The in-flight frame at the time of a connection drop is requeued on
    /// the next reconnect so a single network blip does not lose a message.</item>
    /// <item>Connection state changes are observable via <see cref="StateChanged"/>.</item>
    /// </list>
    /// </summary>
    public sealed class TcpMessagingSession : IDisposable, IConnectionStateObserver
    {
        private readonly ISubject<IncomingTransportMessage> _incomingMessages;
        private readonly CancellationTokenSource _shutdown;
        private readonly ConcurrentDictionary<string, TcpConnectionContext> _connectionsBySessionId;
        private readonly ConcurrentDictionary<long, TcpConnectionContext> _connections;
        private readonly TcpSessionOptions _options;
        private readonly ResiliencyOptions _resiliency;
        private readonly IRetryPolicy _retryPolicy;
        private readonly IClock _clock;
        private readonly BehaviorSubject<ConnectionState> _stateSubject;
        private readonly ChannelWriter<byte[]> _clientOutboundWriter;
        private readonly ChannelReader<byte[]> _clientOutboundReader;
        private readonly object _clientInflightLock = new object();
        private TcpListener _listener;
        private Task _supervisorTask;
        private byte[] _clientInflightFrame;
        private long _connectionCounter;
        private int _disposed;

        public TcpMessagingSession(TcpSessionOptions options)
            : this(options, retryPolicy: null, clock: null)
        {
        }

        public TcpMessagingSession(
            TcpSessionOptions options,
            IRetryPolicy retryPolicy,
            IClock clock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _resiliency = options.Resiliency ?? ResiliencyOptions.Default;
            _retryPolicy = retryPolicy ?? new PollyRetryPolicy(_resiliency);
            _clock = clock ?? SystemClock.Instance;
            _incomingMessages = Subject.Synchronize(new Subject<IncomingTransportMessage>());
            _shutdown = new CancellationTokenSource();
            _connectionsBySessionId = new ConcurrentDictionary<string, TcpConnectionContext>(StringComparer.Ordinal);
            _connections = new ConcurrentDictionary<long, TcpConnectionContext>();
            _stateSubject = new BehaviorSubject<ConnectionState>(ConnectionState.Initializing);

            if (_options.Mode == TcpSessionMode.Server)
            {
                _clientOutboundWriter = null;
                _clientOutboundReader = null;
                StartServer();
            }
            else
            {
                var channel = CreateBoundedChannel<byte[]>(_resiliency, singleReader: true);
                _clientOutboundWriter = channel.Writer;
                _clientOutboundReader = channel.Reader;
                _supervisorTask = Task.Run(() => SuperviseClientAsync(_shutdown.Token), CancellationToken.None);
            }
        }

        public string SessionId => _options.SessionId;

        public ConnectionState CurrentState => _stateSubject.Value;

        public IObservable<ConnectionState> StateChanged => _stateSubject.AsObservable();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _shutdown.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _clientOutboundWriter?.TryComplete();

            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }

            if (_supervisorTask != null)
            {
                try
                {
                    _supervisorTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }

            UpdateState(ConnectionState.Closed);

            try
            {
                _stateSubject.OnCompleted();
            }
            catch
            {
            }
            try
            {
                _stateSubject.Dispose();
            }
            catch
            {
            }
            try
            {
                _incomingMessages.OnCompleted();
            }
            catch
            {
            }
            try
            {
                _shutdown.Dispose();
            }
            catch
            {
            }
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
            TrySend(endpointDetails, message, targetSessionId);
        }

        internal SendResult TrySend(IEndpointDetails endpointDetails, IMessage message, string targetSessionId)
        {
            if (_shutdown.IsCancellationRequested)
            {
                return SendResult.ShuttingDown;
            }

            var envelope = TransportEnvelopeFactory.FromMessage(endpointDetails.Address, message, targetSessionId);
            var frame = TransportTcpFraming.SerializeLengthPrefixed(envelope);

            if (_options.Mode == TcpSessionMode.Client)
            {
                return _clientOutboundWriter != null && _clientOutboundWriter.TryWrite(frame)
                    ? SendResult.Buffered
                    : SendResult.QueueFull;
            }

            if (!string.IsNullOrEmpty(targetSessionId))
            {
                if (!_connectionsBySessionId.TryGetValue(targetSessionId, out var targeted))
                {
                    return SendResult.NoSuchTarget;
                }

                return targeted.TryEnqueue(frame) ? SendResult.Buffered : SendResult.QueueFull;
            }

            if (_connections.IsEmpty)
            {
                return SendResult.NoSuchTarget;
            }

            var anyAccepted = false;
            var anyRejected = false;
            foreach (var connection in _connections.Values)
            {
                if (connection.TryEnqueue(frame))
                {
                    anyAccepted = true;
                }
                else
                {
                    anyRejected = true;
                }
            }

            if (anyAccepted)
            {
                return SendResult.Buffered;
            }

            return anyRejected ? SendResult.QueueFull : SendResult.NoSuchTarget;
        }

        private static Channel<T> CreateBoundedChannel<T>(ResiliencyOptions options, bool singleReader)
        {
            var capacity = options.OutboundQueueCapacity > 0 ? options.OutboundQueueCapacity : 1024;
            var fullMode = options.DropOldestWhenQueueFull
                ? BoundedChannelFullMode.DropOldest
                : BoundedChannelFullMode.Wait;
            return Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = fullMode,
                SingleReader = singleReader,
                SingleWriter = false,
            });
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
                while (!connection.IsBroken)
                {
                    var envelope = await TransportTcpFraming.ReadEnvelopeAsync(connection.Stream, connection.LinkedToken).ConfigureAwait(false);
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
                connection.SignalBroken();
            }
        }

        private TcpConnectionContext CreateConnectionContext(
            TcpClient client,
            ChannelReader<byte[]> outboundReader,
            ChannelWriter<byte[]> outboundWriter,
            Action<byte[]> requeueInflight)
        {
            var id = Interlocked.Increment(ref _connectionCounter);
            return new TcpConnectionContext(
                id,
                client,
                outboundReader,
                outboundWriter,
                requeueInflight,
                _shutdown.Token);
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
            UpdateState(ConnectionState.Connecting);
            try
            {
                var ipAddress = IPAddress.Parse(_options.Host);
                _listener = new TcpListener(ipAddress, _options.Port);
                _listener.Start();
            }
            catch
            {
                UpdateState(ConnectionState.Closed);
                throw;
            }

            UpdateState(ConnectionState.Connected);
            _supervisorTask = Task.Run(AcceptLoopAsync, CancellationToken.None);
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    TcpClient tcpClient;
                    try
                    {
                        tcpClient = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch
                    {
                        continue;
                    }

                    var perConnectionChannel = CreateBoundedChannel<byte[]>(_resiliency, singleReader: true);
                    var connection = CreateConnectionContext(
                        tcpClient,
                        perConnectionChannel.Reader,
                        perConnectionChannel.Writer,
                        _ => { });
                    _connections[connection.Id] = connection;
                    connection.Start(ProcessConnectionAsync);

                    var conn = connection;
                    _ = conn.WaitForCompletionAsync().ContinueWith(_ =>
                    {
                        perConnectionChannel.Writer.TryComplete();
                        RemoveConnection(conn);
                        conn.Dispose();
                    }, TaskScheduler.Default);
                }
            }
            catch
            {
            }
        }

        private async Task SuperviseClientAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateState(ConnectionState.Connecting);

                TcpClient tcpClient = null;
                var connected = await _retryPolicy.ExecuteAsync(
                    async token =>
                    {
                        TcpClient candidate = null;
                        try
                        {
                            candidate = new TcpClient();
                            await candidate.ConnectAsync(_options.Host, _options.Port, token).ConfigureAwait(false);
                            tcpClient = candidate;
                            return true;
                        }
                        catch
                        {
                            try
                            {
                                candidate?.Dispose();
                            }
                            catch
                            {
                            }
                            return false;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!connected || tcpClient == null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        UpdateState(ConnectionState.Closed);
                        return;
                    }
                    continue;
                }

                var connection = CreateConnectionContext(
                    tcpClient,
                    _clientOutboundReader,
                    _clientOutboundWriter,
                    requeuedFrame =>
                    {
                        lock (_clientInflightLock)
                        {
                            _clientInflightFrame = requeuedFrame;
                        }
                    });

                lock (_clientInflightLock)
                {
                    if (_clientInflightFrame != null)
                    {
                        connection.PrependFrame(_clientInflightFrame);
                        _clientInflightFrame = null;
                    }
                }

                _connections[connection.Id] = connection;
                connection.Start(ProcessConnectionAsync);

                UpdateState(ConnectionState.Connected);
                await connection.WaitForCompletionAsync().ConfigureAwait(false);
                RemoveConnection(connection);
                connection.Dispose();

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                UpdateState(ConnectionState.Disconnected);
                try
                {
                    await _clock.Delay(_resiliency.InitialBackoff, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            UpdateState(ConnectionState.Closed);
        }

        private void UpdateState(ConnectionState newState)
        {
            try
            {
                if (_stateSubject.Value == newState)
                {
                    return;
                }
                _stateSubject.OnNext(newState);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private sealed class TcpConnectionContext : IDisposable
        {
            private readonly Action<byte[]> _onWriteFailureRequeue;
            private readonly TcpClient _tcpClient;
            private readonly TaskCompletionSource<bool> _completion;
            private readonly CancellationTokenSource _connectionCts;
            private readonly CancellationTokenSource _linkedCts;
            private readonly ChannelReader<byte[]> _outboundReader;
            private readonly ChannelWriter<byte[]> _outboundWriter;
            private byte[] _prependFrame;
            private Task _readerTask;
            private Task _writerTask;
            private int _broken;
            private int _disposed;

            public TcpConnectionContext(
                long id,
                TcpClient tcpClient,
                ChannelReader<byte[]> outboundReader,
                ChannelWriter<byte[]> outboundWriter,
                Action<byte[]> onWriteFailureRequeue,
                CancellationToken sessionShutdown)
            {
                Id = id;
                _tcpClient = tcpClient;
                Stream = tcpClient.GetStream();
                _outboundReader = outboundReader;
                _outboundWriter = outboundWriter;
                _onWriteFailureRequeue = onWriteFailureRequeue ?? (_ => { });
                _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _connectionCts = new CancellationTokenSource();
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(sessionShutdown, _connectionCts.Token);
            }

            public long Id { get; }

            public string RemoteSessionId { get; private set; }

            public NetworkStream Stream { get; }

            public CancellationToken LinkedToken => _linkedCts.Token;

            public bool IsBroken => Volatile.Read(ref _broken) != 0;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }
                SignalBroken();
                try
                {
                    _connectionCts.Cancel();
                }
                catch
                {
                }
                try
                {
                    Stream.Dispose();
                }
                catch
                {
                }
                try
                {
                    _tcpClient.Dispose();
                }
                catch
                {
                }
                try
                {
                    _linkedCts.Dispose();
                }
                catch
                {
                }
                try
                {
                    _connectionCts.Dispose();
                }
                catch
                {
                }
            }

            public void PrependFrame(byte[] frame)
            {
                _prependFrame = frame;
            }

            public void SignalBroken()
            {
                if (Interlocked.Exchange(ref _broken, 1) == 0)
                {
                    try
                    {
                        _connectionCts.Cancel();
                    }
                    catch
                    {
                    }
                    _completion.TrySetResult(true);
                }
            }

            public void SetRemoteSessionId(string remoteSessionId)
            {
                RemoteSessionId = remoteSessionId;
            }

            public void Start(Func<TcpConnectionContext, Task> processor)
            {
                _readerTask = Task.Run(() => processor(this), CancellationToken.None);
                _writerTask = Task.Run(() => WriteLoopAsync(_linkedCts.Token), CancellationToken.None);
            }

            public bool TryEnqueue(byte[] frame)
            {
                if (_outboundWriter == null)
                {
                    return false;
                }
                return _outboundWriter.TryWrite(frame);
            }

            public Task WaitForCompletionAsync()
            {
                return _completion.Task;
            }

            private async Task WriteLoopAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var pending = _prependFrame;
                    _prependFrame = null;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        byte[] frame;
                        if (pending != null)
                        {
                            frame = pending;
                            pending = null;
                        }
                        else
                        {
                            try
                            {
                                if (!await _outboundReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    return;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            catch
                            {
                                return;
                            }

                            if (!_outboundReader.TryRead(out frame))
                            {
                                continue;
                            }
                        }

                        try
                        {
                            await Stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
                            await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            _onWriteFailureRequeue(frame);
                            return;
                        }
                        catch
                        {
                            _onWriteFailureRequeue(frame);
                            SignalBroken();
                            return;
                        }
                    }
                }
                finally
                {
                    SignalBroken();
                }
            }
        }
    }
}
