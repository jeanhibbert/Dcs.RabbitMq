using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.IntegrationTests.Infrastructure;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Tcp;

namespace Dcs.Messaging.IntegrationTests.Tcp;

/// <summary>
/// Resiliency tests: client connects before any server exists, the server is
/// torn down and brought back up, and we verify the session reconnects and
/// resumes message flow without throwing.
/// </summary>
public sealed class TcpResiliencyTests
{
    [Fact]
    public async Task ClientConnectsAfterServerStartsLater()
    {
        var port = PortAllocator.GetFreePort();
        var clientId = "tcp-client-" + Guid.NewGuid().ToString("N");

        using var client = new TcpMessagingSessionBuilder(
            "TcpLateClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = clientId,
                Resiliency = TestResiliencyOptions.FastReconnect(),
            }).Session;

        var connectingObserved = client.StateChanged.FirstWithinAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(150);

        using var server = new TcpMessagingSessionBuilder(
            "TcpLateServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Server,
                SessionId = "tcp-late-server",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var connectedTask = client.StateChanged
            .Where(state => state == ConnectionState.Connected)
            .FirstWithinAsync(TimeSpan.FromSeconds(10));

        var observedConnected = await connectedTask;
        Assert.Equal(ConnectionState.Connected, observedConnected);

        client.Dispose();
        server.MessagingTransport.Dispose();

        Assert.True(connectingObserved.IsCompleted || connectingObserved.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ClientReconnectsAfterServerRestartAndResumesReceiving()
    {
        var port = PortAllocator.GetFreePort();

        var server1 = StartServer(port);
        await Task.Delay(150);

        using var clientBuilder = new TcpMessagingSessionBuilder(
            "TcpRecoClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "tcp-reco-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var client = clientBuilder.Session;
        var firstConnect = await client.StateChanged
            .Where(state => state == ConnectionState.Connected)
            .FirstWithinAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ConnectionState.Connected, firstConnect);

        var alertEndpoint = TestEndpoints.Alerts(server1.EndpointDetailsProvider);
        var clientAlertEndpoint = TestEndpoints.Alerts(clientBuilder.EndpointDetailsProvider);

        var firstAlertTask = clientBuilder.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => clientBuilder.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(150);
        SendAlert(server1, alertEndpoint, 1);
        var firstAlert = await firstAlertTask;
        Assert.Equal(1, firstAlert.MessageId);

        server1.MessagingTransport.Dispose();
        await client.StateChanged
            .Where(state => state == ConnectionState.Disconnected || state == ConnectionState.Connecting)
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        using var server2 = StartServer(port);
        var reconnect = await client.StateChanged
            .Where(state => state == ConnectionState.Connected)
            .FirstWithinAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ConnectionState.Connected, reconnect);

        var secondAlertTask = clientBuilder.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => clientBuilder.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(200);
        SendAlert(server2, alertEndpoint, 2);
        var secondAlert = await secondAlertTask;
        Assert.Equal(2, secondAlert.MessageId);
    }

    [Fact]
    public async Task ClientSendBeforeConnectIsBufferedAndDeliveredAfterServerStarts()
    {
        var port = PortAllocator.GetFreePort();

        using var clientBuilder = new TcpMessagingSessionBuilder(
            "TcpBufferingClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "tcp-buffering-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var requestEndpoint = TestEndpoints.PricingRequests(clientBuilder.EndpointDetailsProvider);
        var requestDto = new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "USD", QuoteCcy = "JPY", DecimalPlaces = 3 },
            RequestedBy = "tcp-buffering-client",
            SpotRate = 150.123,
            Tenor = "SPOT",
        };
        var payload = clientBuilder.Serializer.Serialize(requestDto);
        var message = clientBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);

        // Use synchronous TrySend so the message is guaranteed buffered before the server starts.
        var sendResult = clientBuilder.MessagingService.TrySend(message, requestEndpoint);
        Assert.True(sendResult.IsSuccess);

        using var serverBuilder = StartServer(port);

        // Wire the server's request-stream subscription BEFORE the client manages to connect,
        // otherwise the buffered message may arrive on the hot subject before we subscribe.
        var arrivedTcs = new TaskCompletionSource<PricingRequestDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverRequestEndpoint = TestEndpoints.PricingRequests(serverBuilder.EndpointDetailsProvider);
        using var outer = serverBuilder.MessagingService.GetMessageStream(serverRequestEndpoint)
            .Subscribe(stream =>
            {
                var inner = stream.Messages.Subscribe(m =>
                    arrivedTcs.TrySetResult(serverBuilder.Serializer.Deserialize<PricingRequestDto>(m.Payload)));
                streamReady.TrySetResult(true);
            });

        await streamReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await clientBuilder.Session.StateChanged
            .Where(state => state == ConnectionState.Connected)
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        var arrived = await arrivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("USD", arrived.CurrencyPair.BaseCcy);
        Assert.Equal(150.123, arrived.SpotRate, 3);
    }

    [Fact]
    public void ClientSendNeverThrowsWhenServerIsAbsent()
    {
        var port = PortAllocator.GetFreePort();

        using var clientBuilder = new TcpMessagingSessionBuilder(
            "TcpNoThrowClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "tcp-no-throw-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var requestEndpoint = TestEndpoints.PricingRequests(clientBuilder.EndpointDetailsProvider);
        var payload = clientBuilder.Serializer.Serialize(new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "EUR", QuoteCcy = "GBP" },
            RequestedBy = "x",
            SpotRate = 1.0,
            Tenor = "SPOT",
        });

        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                var msg = clientBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);
                clientBuilder.MessagingService.Send(msg, requestEndpoint);
            }
        });

        Assert.Null(ex);
    }

    private static TcpMessagingSessionBuilder StartServer(int port)
    {
        return new TcpMessagingSessionBuilder(
            "TcpResilientServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Server,
                SessionId = "tcp-resilient-server-" + Guid.NewGuid().ToString("N"),
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });
    }

    private static void SendAlert(TcpMessagingSessionBuilder server, IEndpointDetails endpoint, int id)
    {
        var alert = new TestAlertDto { MessageId = id, Message = id.ToString(), ServerName = "srv" };
        var payload = server.Serializer.Serialize(alert);
        var message = server.MessageFactory.Create(payload, TimeSpan.Zero, false);
        server.MessagingService.Send(message, endpoint);
    }
}
