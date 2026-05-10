using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.IntegrationTests.Infrastructure;
using Dcs.Messaging.RequestResponse;
using Dcs.Messaging.Tcp;

namespace Dcs.Messaging.IntegrationTests.Tcp;

/// <summary>
/// End-to-end tests for the TCP transport: a real server and real client
/// exchange real messages over a loopback socket.
/// </summary>
public sealed class TcpEndToEndTests : IDisposable
{
    private readonly int _port;
    private readonly string _serverId;
    private readonly TcpMessagingSessionBuilder _server;

    public TcpEndToEndTests()
    {
        _port = PortAllocator.GetFreePort();
        _serverId = "tcp-server-" + Guid.NewGuid().ToString("N");
        _server = new TcpMessagingSessionBuilder(
            "TcpServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = _port,
                Mode = TcpSessionMode.Server,
                SessionId = _serverId,
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });
    }

    public void Dispose()
    {
        _server.MessagingTransport.Dispose();
    }

    [Fact]
    public async Task ServerCanBroadcastAlertToClient()
    {
        using var client = BuildClient(out var clientId);
        await Task.Delay(150);

        var alertEndpoint = TestEndpoints.Alerts(_server.EndpointDetailsProvider);
        var clientAlertEndpoint = TestEndpoints.Alerts(client.EndpointDetailsProvider);

        var receivedTask = client.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => client.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(200).ConfigureAwait(false);

        var alert = new TestAlertDto { MessageId = 42, Message = "Hello", ServerName = _serverId };
        var serverPayload = _server.Serializer.Serialize(alert);
        var serverMessage = _server.MessageFactory.Create(serverPayload, TimeSpan.Zero, false);
        _server.MessagingService.Send(serverMessage, alertEndpoint);

        var received = await receivedTask;

        Assert.Equal(42, received.MessageId);
        Assert.Equal("Hello", received.Message);
        Assert.Equal(_serverId, received.ServerName);
    }

    [Fact]
    public async Task RequestResponseRoundTripSucceeds()
    {
        using var client = BuildClient(out var clientId);
        await Task.Delay(150);

        var serverRequestEndpoint = TestEndpoints.PricingRequests(_server.EndpointDetailsProvider);
        var serverResponseEndpoint = TestEndpoints.PricingResponses(_server.EndpointDetailsProvider);
        var clientRequestEndpoint = TestEndpoints.PricingRequests(client.EndpointDetailsProvider);
        var clientResponseEndpoint = TestEndpoints.PricingResponses(client.EndpointDetailsProvider);

        using var serverPricing = _server.RequestResponder
            .GetRespondableRequestStream<PricingRequestDto, ForwardPricingDto>(serverRequestEndpoint, serverResponseEndpoint)
            .Subscribe(request =>
            {
                var response = new ForwardPricingDto
                {
                    CurrencyPair = request.Request.CurrencyPair,
                    BidRate = Math.Round(request.Request.SpotRate - 0.001, 5),
                    AskRate = Math.Round(request.Request.SpotRate + 0.001, 5),
                    IsContra = false,
                };
                request.Respond(response);
            });

        await Task.Delay(150);

        var correlationId = Guid.NewGuid().ToString("N");
        var responseTask = client.MessagingService
            .GetMessageStream(clientResponseEndpoint)
            .SelectMany(stream => stream.Messages)
            .Where(message => string.Equals(message.Properties.GetString(TransportPropertyKeys.CorrelationId), correlationId, StringComparison.Ordinal))
            .Select(message => client.Serializer.Deserialize<ForwardPricingDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(150);

        var request = new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "EUR", QuoteCcy = "USD", DecimalPlaces = 5 },
            RequestedBy = clientId,
            SpotRate = 1.10000,
            Tenor = "SPOT",
        };
        var payload = client.Serializer.Serialize(request);
        var message = client.MessageFactory.Create(payload, TimeSpan.FromSeconds(5), false);
        message.Properties.Set(TransportPropertyKeys.CorrelationId, correlationId);
        message.Properties.Set(TransportPropertyKeys.MessageKind, MessageKinds.Request);
        client.MessagingService.Send(message, clientRequestEndpoint);

        var response = await responseTask;
        Assert.Equal("EUR", response.CurrencyPair.BaseCcy);
        Assert.Equal(1.099, response.BidRate, 5);
        Assert.Equal(1.101, response.AskRate, 5);
    }

    [Fact]
    public async Task ManyMessagesArriveInOrder()
    {
        using var client = BuildClient(out _);
        await Task.Delay(150);

        var alertEndpoint = TestEndpoints.Alerts(_server.EndpointDetailsProvider);
        var clientAlertEndpoint = TestEndpoints.Alerts(client.EndpointDetailsProvider);
        const int count = 100;

        var receivedTask = client.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => client.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .CollectWithinAsync(count, TimeSpan.FromSeconds(10));

        await Task.Delay(200);

        // Use TrySend (synchronous) to guarantee enqueue order.
        // The async Send path schedules sends on the thread pool which can reorder them.
        for (var i = 0; i < count; i++)
        {
            var alert = new TestAlertDto { MessageId = i, Message = i.ToString(), ServerName = _serverId };
            var payload = _server.Serializer.Serialize(alert);
            var message = _server.MessageFactory.Create(payload, TimeSpan.Zero, false);
            _server.MessagingService.TrySend(message, alertEndpoint);
        }

        var received = await receivedTask;
        Assert.Equal(count, received.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i, received[i].MessageId);
        }
    }

    private TcpMessagingSessionBuilder BuildClient(out string clientId)
    {
        clientId = "tcp-client-" + Guid.NewGuid().ToString("N");
        return new TcpMessagingSessionBuilder(
            "TcpClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = _port,
                Mode = TcpSessionMode.Client,
                SessionId = clientId,
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });
    }
}
