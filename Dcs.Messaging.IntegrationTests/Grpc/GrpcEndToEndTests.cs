using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.IntegrationTests.Infrastructure;

namespace Dcs.Messaging.IntegrationTests.Grpc;

/// <summary>
/// End-to-end tests for the gRPC transport using a real Kestrel host and a
/// real <see cref="Grpc.Net.Client.GrpcChannel"/>.
/// </summary>
public sealed class GrpcEndToEndTests : IDisposable
{
    private readonly int _port;
    private readonly string _baseUrl;
    private readonly string _serverId;
    private readonly GrpcMessagingSessionBuilder _server;

    public GrpcEndToEndTests()
    {
        _port = PortAllocator.GetFreePort();
        _baseUrl = $"http://127.0.0.1:{_port}";
        _serverId = "grpc-server-" + Guid.NewGuid().ToString("N");
        _server = new GrpcMessagingSessionBuilder(
            "GrpcServer",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = _baseUrl,
                Mode = GrpcSessionMode.Server,
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
        await Task.Delay(250);
        using var client = BuildClient(out _);
        await Task.Delay(250);

        var alertEndpoint = TestEndpoints.Alerts(_server.EndpointDetailsProvider);
        var clientAlertEndpoint = TestEndpoints.Alerts(client.EndpointDetailsProvider);

        var receivedTask = client.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => client.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(250);

        var alert = new TestAlertDto { MessageId = 99, Message = "Grpc-Hello", ServerName = _serverId };
        var serverPayload = _server.Serializer.Serialize(alert);
        var serverMessage = _server.MessageFactory.Create(serverPayload, TimeSpan.Zero, false);
        _server.MessagingService.Send(serverMessage, alertEndpoint);

        var received = await receivedTask;

        Assert.Equal(99, received.MessageId);
        Assert.Equal("Grpc-Hello", received.Message);
        Assert.Equal(_serverId, received.ServerName);
    }

    [Fact]
    public async Task RequestResponseRoundTripSucceeds()
    {
        await Task.Delay(250);
        using var client = BuildClient(out var clientId);
        await Task.Delay(250);

        var serverRequestEndpoint = TestEndpoints.PricingRequests(_server.EndpointDetailsProvider);
        var serverResponseEndpoint = TestEndpoints.PricingResponses(_server.EndpointDetailsProvider);
        var clientRequestEndpoint = TestEndpoints.PricingRequests(client.EndpointDetailsProvider);
        var clientResponseEndpoint = TestEndpoints.PricingResponses(client.EndpointDetailsProvider);

        using var serverPricing = _server.RequestResponder
            .GetRespondableRequestStream<PricingRequestDto, ForwardPricingDto>(serverRequestEndpoint, serverResponseEndpoint)
            .Subscribe(request =>
            {
                request.Respond(new ForwardPricingDto
                {
                    CurrencyPair = request.Request.CurrencyPair,
                    BidRate = Math.Round(request.Request.SpotRate - 0.001, 5),
                    AskRate = Math.Round(request.Request.SpotRate + 0.001, 5),
                    IsContra = false,
                });
            });

        await Task.Delay(250);

        var correlationId = Guid.NewGuid().ToString("N");
        var responseTask = client.MessagingService
            .GetMessageStream(clientResponseEndpoint)
            .SelectMany(stream => stream.Messages)
            .Where(message => string.Equals(message.Properties.GetString(TransportPropertyKeys.CorrelationId), correlationId, StringComparison.Ordinal))
            .Select(message => client.Serializer.Deserialize<ForwardPricingDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(250);

        var request = new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "GBP", QuoteCcy = "USD", DecimalPlaces = 5 },
            RequestedBy = clientId,
            SpotRate = 1.25000,
            Tenor = "1M",
        };
        var payload = client.Serializer.Serialize(request);
        var message = client.MessageFactory.Create(payload, TimeSpan.FromSeconds(5), false);
        message.Properties.Set(TransportPropertyKeys.CorrelationId, correlationId);
        message.Properties.Set(TransportPropertyKeys.MessageKind, MessageKinds.Request);
        client.MessagingService.Send(message, clientRequestEndpoint);

        var response = await responseTask;
        Assert.Equal("GBP", response.CurrencyPair.BaseCcy);
        Assert.Equal(1.249, response.BidRate, 5);
        Assert.Equal(1.251, response.AskRate, 5);
    }

    private GrpcMessagingSessionBuilder BuildClient(out string clientId)
    {
        clientId = "grpc-client-" + Guid.NewGuid().ToString("N");
        return new GrpcMessagingSessionBuilder(
            "GrpcClient",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = _baseUrl,
                Mode = GrpcSessionMode.Client,
                SessionId = clientId,
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });
    }
}
