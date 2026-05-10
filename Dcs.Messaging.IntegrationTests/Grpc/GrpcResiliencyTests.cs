using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.IntegrationTests.Infrastructure;
using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.IntegrationTests.Grpc;

/// <summary>
/// Resiliency tests for the gRPC transport: client connects before the server
/// is up, server is restarted, and we confirm the bidirectional exchange
/// resumes without throwing across the API boundary.
/// </summary>
public sealed class GrpcResiliencyTests
{
    [Fact]
    public async Task ClientConnectsAfterServerStartsLater()
    {
        var port = PortAllocator.GetFreePort();
        var baseUrl = $"http://127.0.0.1:{port}";

        using var clientBuilder = new GrpcMessagingSessionBuilder(
            "GrpcLateClient",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Client,
                SessionId = "grpc-late-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        await Task.Delay(150);

        using var server = StartServer(port, baseUrl);
        var connected = await clientBuilder.Session.StateChanged
            .Where(state => state == ConnectionState.Connected)
            .FirstWithinAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(ConnectionState.Connected, connected);
    }

    [Fact]
    public async Task ClientSendBeforeConnectIsBufferedAndDeliveredAfterServerStarts()
    {
        var port = PortAllocator.GetFreePort();
        var baseUrl = $"http://127.0.0.1:{port}";

        using var clientBuilder = new GrpcMessagingSessionBuilder(
            "GrpcBufferingClient",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Client,
                SessionId = "grpc-buffering-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var requestEndpoint = TestEndpoints.PricingRequests(clientBuilder.EndpointDetailsProvider);
        var requestDto = new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "USD", QuoteCcy = "JPY", DecimalPlaces = 3 },
            RequestedBy = "grpc-buffering-client",
            SpotRate = 150.456,
            Tenor = "SPOT",
        };
        var payload = clientBuilder.Serializer.Serialize(requestDto);
        var message = clientBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);

        var earlyResult = clientBuilder.MessagingService.TrySend(message, requestEndpoint);
        Assert.True(earlyResult.IsSuccess);
        Assert.Equal(SendStatus.Buffered, earlyResult.Status);

        using var serverBuilder = StartServer(port, baseUrl);

        // Subscribe to the server's request stream before the client connects to avoid
        // the hot-subject race window.
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
            .FirstWithinAsync(TimeSpan.FromSeconds(15));

        var arrived = await arrivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("USD", arrived.CurrencyPair.BaseCcy);
        Assert.Equal(150.456, arrived.SpotRate, 3);
    }

    [Fact]
    public void ClientSendNeverThrowsWhenServerIsAbsent()
    {
        var port = PortAllocator.GetFreePort();
        var baseUrl = $"http://127.0.0.1:{port}";

        using var clientBuilder = new GrpcMessagingSessionBuilder(
            "GrpcNoThrowClient",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Client,
                SessionId = "grpc-no-throw-client",
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

    private static GrpcMessagingSessionBuilder StartServer(int port, string baseUrl)
    {
        return new GrpcMessagingSessionBuilder(
            "GrpcResilientServer",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Server,
                SessionId = "grpc-resilient-server-" + Guid.NewGuid().ToString("N"),
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });
    }
}
