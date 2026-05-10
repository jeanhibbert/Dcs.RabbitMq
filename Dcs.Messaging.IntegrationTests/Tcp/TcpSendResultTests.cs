using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.IntegrationTests.Infrastructure;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Tcp;

namespace Dcs.Messaging.IntegrationTests.Tcp;

/// <summary>
/// Verifies that <see cref="IMessagingService.TrySend"/> reports outcomes via
/// <see cref="SendResult"/> rather than throwing on transport failure.
/// </summary>
public sealed class TcpSendResultTests
{
    [Fact]
    public void TrySendReturnsBufferedWhenClientNotYetConnected()
    {
        var port = PortAllocator.GetFreePort();
        using var clientBuilder = new TcpMessagingSessionBuilder(
            "TcpResultClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "tcp-result-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var endpoint = clientBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestPricingRequests);
        var payload = clientBuilder.Serializer.Serialize(new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "EUR", QuoteCcy = "USD" },
            RequestedBy = "x",
            SpotRate = 1.0,
            Tenor = "SPOT",
        });
        var message = clientBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);

        var result = clientBuilder.MessagingService.TrySend(message, endpoint);
        Assert.Equal(SendStatus.Buffered, result.Status);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TrySendReturnsQueueFullOnClientWhenBackPressureHits()
    {
        var port = PortAllocator.GetFreePort();
        using var clientBuilder = new TcpMessagingSessionBuilder(
            "TcpQueueFullClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "tcp-queue-full-client",
                Resiliency = TestResiliencyOptions.TinyQueue(2),
            });

        var endpoint = clientBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestPricingRequests);
        var payload = clientBuilder.Serializer.Serialize(new PricingRequestDto
        {
            CurrencyPair = new CurrencyPairDto { BaseCcy = "EUR", QuoteCcy = "USD" },
            RequestedBy = "x",
            SpotRate = 1.0,
            Tenor = "SPOT",
        });

        var lastResult = SendResult.Buffered;
        for (var i = 0; i < 50; i++)
        {
            var message = clientBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);
            lastResult = clientBuilder.MessagingService.TrySend(message, endpoint);
            if (lastResult.Status == SendStatus.QueueFull)
            {
                break;
            }
        }

        Assert.Equal(SendStatus.QueueFull, lastResult.Status);
        Assert.False(lastResult.IsSuccess);
    }

    [Fact]
    public void ServerTrySendWithoutClientsReturnsNoSuchTarget()
    {
        var port = PortAllocator.GetFreePort();
        using var serverBuilder = new TcpMessagingSessionBuilder(
            "TcpAloneServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Server,
                SessionId = "tcp-alone-server",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        var endpoint = serverBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestAlerts);
        var payload = serverBuilder.Serializer.Serialize(new TestAlertDto { MessageId = 1, Message = "hi", ServerName = "srv" });
        var message = serverBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);

        var result = serverBuilder.MessagingService.TrySend(message, endpoint);
        Assert.Equal(SendStatus.NoSuchTarget, result.Status);
        Assert.False(result.IsSuccess);
    }
}
