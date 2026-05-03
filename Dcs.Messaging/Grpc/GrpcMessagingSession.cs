using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Transport;
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
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dcs.RabbitMq.Messaging.Grpc
{
    public sealed class GrpcMessagingSession : IDisposable
    {
        private readonly ISubject<IncomingTransportMessage> _incomingMessages;
        private readonly CancellationTokenSource _shutdown;
        private readonly ConcurrentDictionary<string, GrpcConnectionContext> _connectionsBySessionId;
        private readonly ConcurrentDictionary<long, GrpcConnectionContext> _connections;
        private readonly GrpcSessionOptions _options;
        private readonly long _clientConnectionId = 1;
        private WebApplication _app;
        private Task _clientExchangeTask;
        private GrpcConnectionContext _clientConnection;
        private ChannelWriter<TransportEnvelope> _clientOutgoing;
        private GrpcChannel _grpcChannel;
        private long _connectionCounter;

        public GrpcMessagingSession(GrpcSessionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _incomingMessages = Subject.Synchronize(new Subject<IncomingTransportMessage>());
            _shutdown = new CancellationTokenSource();
            _connectionsBySessionId = new ConcurrentDictionary<string, GrpcConnectionContext>(StringComparer.Ordinal);
            _connections = new ConcurrentDictionary<long, GrpcConnectionContext>();

            if (_options.Mode == GrpcSessionMode.Server)
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

            if (_clientExchangeTask != null)
            {
                try
                {
                    _clientExchangeTask.GetAwaiter().GetResult();
                }
                catch
                {
                }
            }

            _grpcChannel?.Dispose();
            _grpcChannel = null;

            _incomingMessages.OnCompleted();
            _shutdown.Dispose();
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
            var envelope = TransportEnvelopeFactory.FromMessage(endpointDetails.Address, message, targetSessionId);

            if (_options.Mode == GrpcSessionMode.Client)
            {
                if (_clientOutgoing == null)
                {
                    throw new InvalidOperationException("Client is not connected to a server endpoint.");
                }

                if (!_clientOutgoing.TryWrite(envelope))
                {
                    throw new InvalidOperationException("Unable to send: gRPC stream is not available.");
                }

                return;
            }

            if (!string.IsNullOrEmpty(targetSessionId))
            {
                if (_connectionsBySessionId.TryGetValue(targetSessionId, out var connection))
                {
                    connection.TrySend(envelope);
                }

                return;
            }

            foreach (var connection in _connections.Values)
            {
                connection.TrySend(envelope);
            }
        }

        private async IAsyncEnumerable<TransportEnvelope> AttachConnectionCore(
            IAsyncEnumerable<TransportEnvelope> incoming,
            CallContext context)
        {
            var outgoing = Channel.CreateUnbounded<TransportEnvelope>();
            var id = Interlocked.Increment(ref _connectionCounter);
            var connection = new GrpcConnectionContext(id, outgoing.Writer);
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
                    finally
                    {
                        outgoing.Writer.TryComplete();
                        RemoveConnection(connection);
                    }
                },
                CancellationToken.None);

            try
            {
                await foreach (var env in outgoing.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return env;
                }
            }
            finally
            {
                await readTask.ConfigureAwait(false);
            }
        }

        private void ConnectClient()
        {
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

            var outgoingChannel = Channel.CreateUnbounded<TransportEnvelope>();
            _clientOutgoing = outgoingChannel.Writer;
            _clientConnection = new GrpcConnectionContext(_clientConnectionId, outgoingChannel.Writer);

            _clientExchangeTask = Task.Run(() => RunClientExchangeAsync(outgoingChannel, _shutdown.Token), _shutdown.Token);
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

        private async Task RunClientExchangeAsync(Channel<TransportEnvelope> outgoingChannel, CancellationToken cancellationToken)
        {
            var client = _grpcChannel.CreateGrpcService<IMessagingTransportService>();

            async IAsyncEnumerable<TransportEnvelope> OutgoingMessages()
            {
                await foreach (var env in outgoingChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return env;
                }
            }

            try
            {
                await foreach (var env in client.Exchange(OutgoingMessages(), new CallContext(new CallOptions(cancellationToken: cancellationToken))).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    HandleIncomingEnvelope(_clientConnection, env);
                }
            }
            finally
            {
                outgoingChannel.Writer.TryComplete();
            }
        }

        private void StartServer()
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

        private sealed class GrpcConnectionContext
        {
            public GrpcConnectionContext(long id, ChannelWriter<TransportEnvelope> outgoingWriter)
            {
                Id = id;
                OutgoingWriter = outgoingWriter;
            }

            public long Id { get; }

            public ChannelWriter<TransportEnvelope> OutgoingWriter { get; }

            public string RemoteSessionId { get; private set; }

            public void SetRemoteSessionId(string remoteSessionId)
            {
                RemoteSessionId = remoteSessionId;
            }

            public void TrySend(TransportEnvelope envelope)
            {
                OutgoingWriter.TryWrite(envelope);
            }
        }
    }
}
