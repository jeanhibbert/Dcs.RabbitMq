using Dcs.Messaging;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Transport;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dcs.Messaging.Grpc
{
    /// <summary>
    /// gRPC transport session with built-in resiliency:
    /// <list type="bullet">
    /// <item>Per-connection bounded outbound queues. Sends never throw on transport failure.</item>
    /// <item>Client mode automatically restarts the bidirectional <c>Exchange</c>
    /// stream on <see cref="RpcException"/> with exponential backoff via <see cref="IRetryPolicy"/>.</item>
    /// <item>The in-flight envelope at the time of an exchange drop is requeued on
    /// the next reconnect so a single network blip does not lose a message.</item>
    /// <item>Connection state changes are observable via <see cref="StateChanged"/>.</item>
    /// </list>
    /// </summary>
    public sealed class GrpcMessagingSession : IDisposable, IConnectionStateObserver
    {
        private readonly ISubject<IncomingTransportMessage> _incomingMessages;
        private readonly CancellationTokenSource _shutdown;
        private readonly ConcurrentDictionary<string, GrpcConnectionContext> _connectionsBySessionId;
        private readonly ConcurrentDictionary<long, GrpcConnectionContext> _connections;
        private readonly GrpcSessionOptions _options;
        private readonly ResiliencyOptions _resiliency;
        private readonly IRetryPolicy _retryPolicy;
        private readonly IClock _clock;
        private readonly BehaviorSubject<ConnectionState> _stateSubject;
        private readonly object _clientInflightLock = new object();
        private readonly long _clientConnectionId = 1;
        private WebApplication _app;
        private Task _supervisorTask;
        private GrpcConnectionContext _clientConnection;
        private ChannelWriter<TransportEnvelope> _clientOutgoingWriter;
        private ChannelReader<TransportEnvelope> _clientOutgoingReader;
        private GrpcChannel _grpcChannel;
        private TransportEnvelope _clientInflight;
        private long _connectionCounter;
        private int _disposed;

        public GrpcMessagingSession(GrpcSessionOptions options)
            : this(options, retryPolicy: null, clock: null)
        {
        }

        public GrpcMessagingSession(
            GrpcSessionOptions options,
            IRetryPolicy retryPolicy,
            IClock clock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _resiliency = options.Resiliency ?? ResiliencyOptions.Default;
            _retryPolicy = retryPolicy ?? new PollyRetryPolicy(_resiliency);
            _clock = clock ?? SystemClock.Instance;
            _incomingMessages = Subject.Synchronize(new Subject<IncomingTransportMessage>());
            _shutdown = new CancellationTokenSource();
            _connectionsBySessionId = new ConcurrentDictionary<string, GrpcConnectionContext>(StringComparer.Ordinal);
            _connections = new ConcurrentDictionary<long, GrpcConnectionContext>();
            _stateSubject = new BehaviorSubject<ConnectionState>(ConnectionState.Initializing);

            if (_options.Mode == GrpcSessionMode.Server)
            {
                StartServer();
            }
            else
            {
                var channel = CreateBoundedChannel<TransportEnvelope>(_resiliency, singleReader: true);
                _clientOutgoingWriter = channel.Writer;
                _clientOutgoingReader = channel.Reader;
                _clientConnection = new GrpcConnectionContext(_clientConnectionId, _clientOutgoingWriter, _resiliency);
                _connections[_clientConnection.Id] = _clientConnection;
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

            if (_app != null)
            {
                try
                {
                    _app.StopAsync().GetAwaiter().GetResult();
                }
                catch
                {
                }

                try
                {
                    _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                }

                _app = null;
            }

            _clientOutgoingWriter?.TryComplete();

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

            try
            {
                _grpcChannel?.Dispose();
            }
            catch
            {
            }
            _grpcChannel = null;

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

        internal IAsyncEnumerable<TransportEnvelope> AttachConnection(
            IAsyncEnumerable<TransportEnvelope> incoming,
            CallContext context)
        {
            return AttachConnectionCore(incoming, context);
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

            if (_options.Mode == GrpcSessionMode.Client)
            {
                return _clientOutgoingWriter != null && _clientOutgoingWriter.TryWrite(envelope)
                    ? SendResult.Buffered
                    : SendResult.QueueFull;
            }

            if (!string.IsNullOrEmpty(targetSessionId))
            {
                if (!_connectionsBySessionId.TryGetValue(targetSessionId, out var targeted))
                {
                    return SendResult.NoSuchTarget;
                }
                return targeted.TryEnqueue(envelope) ? SendResult.Buffered : SendResult.QueueFull;
            }

            if (_connections.IsEmpty)
            {
                return SendResult.NoSuchTarget;
            }

            var anyAccepted = false;
            var anyRejected = false;
            foreach (var connection in _connections.Values)
            {
                if (connection.TryEnqueue(envelope))
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

        private async IAsyncEnumerable<TransportEnvelope> AttachConnectionCore(
            IAsyncEnumerable<TransportEnvelope> incoming,
            CallContext context)
        {
            var perConnectionChannel = CreateBoundedChannel<TransportEnvelope>(_resiliency, singleReader: true);
            var id = Interlocked.Increment(ref _connectionCounter);
            var connection = new GrpcConnectionContext(id, perConnectionChannel.Writer, _resiliency);
            _connections[id] = connection;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, context.CancellationToken);
            var cancellationToken = linked.Token;

            var readTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await foreach (var env in incoming.WithCancellation(cancellationToken).ConfigureAwait(false))
                        {
                            HandleIncomingEnvelope(connection, env);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch
                    {
                    }
                    finally
                    {
                        perConnectionChannel.Writer.TryComplete();
                        RemoveConnection(connection);
                    }
                },
                CancellationToken.None);

            try
            {
                await foreach (var env in perConnectionChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return env;
                }
            }
            finally
            {
                try
                {
                    await readTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private void HandleIncomingEnvelope(GrpcConnectionContext connection, TransportEnvelope envelope)
        {
            if (!string.IsNullOrWhiteSpace(envelope.SenderSessionId))
            {
                connection.SetRemoteSessionId(envelope.SenderSessionId);
                _connectionsBySessionId[envelope.SenderSessionId] = connection;
            }

            var message = TransportEnvelopeFactory.ToMessage(envelope);
            _incomingMessages.OnNext(new IncomingTransportMessage(envelope.EndpointAddress, envelope.TargetSessionId, message));
        }

        private void RemoveConnection(GrpcConnectionContext connection)
        {
            _connections.TryRemove(connection.Id, out _);
            if (!string.IsNullOrWhiteSpace(connection.RemoteSessionId))
            {
                _connectionsBySessionId.TryRemove(connection.RemoteSessionId, out _);
            }
        }

        private async Task SuperviseClientAsync(CancellationToken cancellationToken)
        {
            EnsureClientChannel();

            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateState(ConnectionState.Connecting);

                var connected = await _retryPolicy.ExecuteAsync(
                    async token =>
                    {
                        try
                        {
                            await RunClientExchangeOnceAsync(token).ConfigureAwait(false);
                            return true;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!connected)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    continue;
                }

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

        private void EnsureClientChannel()
        {
            if (_grpcChannel != null)
            {
                return;
            }

            if (_options.EnableHttp2Unencrypted)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            _grpcChannel = GrpcChannel.ForAddress(
                _options.BaseUrl,
                new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                    },
                });
        }

        private async Task RunClientExchangeOnceAsync(CancellationToken cancellationToken)
        {
            EnsureClientChannel();
            var transport = _grpcChannel.CreateGrpcService<IMessagingTransportService>();

            TransportEnvelope inflight;
            lock (_clientInflightLock)
            {
                inflight = _clientInflight;
                _clientInflight = null;
            }

            async IAsyncEnumerable<TransportEnvelope> OutgoingMessages()
            {
                if (inflight != null)
                {
                    var first = inflight;
                    inflight = null;
                    yield return first;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    TransportEnvelope env;
                    var hasMore = await _clientOutgoingReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                    if (!hasMore)
                    {
                        yield break;
                    }
                    if (!_clientOutgoingReader.TryRead(out env))
                    {
                        continue;
                    }

                    lock (_clientInflightLock)
                    {
                        _clientInflight = env;
                    }

                    yield return env;

                    lock (_clientInflightLock)
                    {
                        if (ReferenceEquals(_clientInflight, env))
                        {
                            _clientInflight = null;
                        }
                    }
                }
            }

            UpdateState(ConnectionState.Connected);

            try
            {
                await foreach (var env in transport
                    .Exchange(OutgoingMessages(), new CallContext(new CallOptions(cancellationToken: cancellationToken)))
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    HandleIncomingEnvelope(_clientConnection, env);
                }
            }
            catch
            {
                if (inflight != null)
                {
                    lock (_clientInflightLock)
                    {
                        _clientInflight ??= inflight;
                    }
                }
                throw;
            }
        }

        private void StartServer()
        {
            UpdateState(ConnectionState.Connecting);
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.Services.AddSingleton<GrpcMessagingSession>(_ => this);
                builder.Services.AddSingleton<MessagingTransportGrpcService>();
                builder.Services.AddGrpc();
                builder.Services.AddCodeFirstGrpc();
                ConfigureKestrel(builder, _options.BaseUrl);

                _app = builder.Build();
                _app.MapGrpcService<MessagingTransportGrpcService>();
                _ = _app.RunAsync();
                UpdateState(ConnectionState.Connected);
            }
            catch
            {
                UpdateState(ConnectionState.Closed);
                throw;
            }
        }

        private static void ConfigureKestrel(WebApplicationBuilder builder, string baseUrl)
        {
            var uri = new Uri(baseUrl);
            if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("BaseUrl must use http or https scheme.", nameof(baseUrl));
            }

            var port = uri.IsDefaultPort ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;

            builder.Services.Configure<KestrelServerOptions>(
                options =>
                {
                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Equals("127.0.0.1", StringComparison.Ordinal)
                        || uri.Host.Equals("::1", StringComparison.Ordinal))
                    {
                        options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                    }
                    else
                    {
                        options.ListenAnyIP(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                    }
                });
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

        private sealed class GrpcConnectionContext
        {
            private readonly ChannelWriter<TransportEnvelope> _outgoingWriter;

            public GrpcConnectionContext(long id, ChannelWriter<TransportEnvelope> outgoingWriter, ResiliencyOptions resiliency)
            {
                Id = id;
                _outgoingWriter = outgoingWriter;
            }

            public long Id { get; }

            public string RemoteSessionId { get; private set; }

            public void SetRemoteSessionId(string remoteSessionId)
            {
                RemoteSessionId = remoteSessionId;
            }

            public bool TryEnqueue(TransportEnvelope envelope)
            {
                return _outgoingWriter != null && _outgoingWriter.TryWrite(envelope);
            }
        }
    }
}
