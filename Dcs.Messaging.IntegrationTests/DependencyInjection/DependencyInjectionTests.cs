using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Common.DependencyInjection;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.IntegrationTests.Infrastructure;
using Dcs.Messaging.RequestResponse;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Serialization;
using Dcs.Messaging.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace Dcs.Messaging.IntegrationTests.DependencyInjection;

/// <summary>
/// Verifies that <see cref="MessagingServiceCollectionExtensions"/> wires the
/// framework into a standard <see cref="ServiceCollection"/> and that resolved
/// services are usable for end-to-end communication.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void TcpAddDcsTcpMessagingResolvesAllAbstractions()
    {
        var port = PortAllocator.GetFreePort();
        var services = new ServiceCollection();
        services.AddDcsMessagingResiliency(TestResiliencyOptions.FastReconnect());
        services.AddDcsTcpMessaging(
            "DiTcpServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Server,
                SessionId = "di-tcp-server",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        using var provider = services.BuildServiceProvider();

        var builder = provider.GetRequiredService<IMessagingSessionBuilder>();
        Assert.NotNull(builder.MessagingService);
        Assert.NotNull(builder.RequestResponder);
        Assert.NotNull(builder.MessageFactory);
        Assert.NotNull(builder.Serializer);
        Assert.NotNull(builder.EndpointDetailsFactory);
        Assert.NotNull(builder.EndpointDetailsProvider);

        Assert.IsAssignableFrom<TcpMessagingSession>(builder.MessagingTransport);

        Assert.Same(provider.GetRequiredService<IMessagingService>(), builder.MessagingService);
        Assert.Same(provider.GetRequiredService<IRequestResponder>(), builder.RequestResponder);
        Assert.Same(provider.GetRequiredService<IBinarySerializer>(), builder.Serializer);
        Assert.Same(provider.GetRequiredService<IMessageFactory>(), builder.MessageFactory);

        Assert.IsType<PollyRetryPolicy>(provider.GetRequiredService<IRetryPolicy>());
        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());

        builder.MessagingTransport.Dispose();
    }

    [Fact]
    public void GrpcAddDcsGrpcMessagingResolvesAllAbstractions()
    {
        var port = PortAllocator.GetFreePort();
        var services = new ServiceCollection();
        services.AddDcsMessagingResiliency(TestResiliencyOptions.FastReconnect());
        services.AddDcsGrpcMessaging(
            "DiGrpcServer",
            new TestEndpointDetailsProvider(),
            new GrpcSessionOptions
            {
                BaseUrl = $"http://127.0.0.1:{port}",
                Mode = GrpcSessionMode.Server,
                SessionId = "di-grpc-server",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        using var provider = services.BuildServiceProvider();

        var builder = provider.GetRequiredService<IMessagingSessionBuilder>();
        Assert.NotNull(builder.MessagingService);
        Assert.NotNull(builder.RequestResponder);
        Assert.IsAssignableFrom<GrpcMessagingSession>(builder.MessagingTransport);

        builder.MessagingTransport.Dispose();
    }

    [Fact]
    public async Task TcpEndToEndUsingResolvedServices()
    {
        var port = PortAllocator.GetFreePort();
        var services = new ServiceCollection();
        services.AddDcsMessagingResiliency(TestResiliencyOptions.FastReconnect());
        services.AddDcsTcpMessaging(
            "DiTcpE2EServer",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Server,
                SessionId = "di-tcp-e2e-server",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        await using var serverProvider = services.BuildServiceProvider();
        var serverBuilder = serverProvider.GetRequiredService<IMessagingSessionBuilder>();

        var clientServices = new ServiceCollection();
        clientServices.AddDcsMessagingResiliency(TestResiliencyOptions.FastReconnect());
        clientServices.AddDcsTcpMessaging(
            "DiTcpE2EClient",
            new TestEndpointDetailsProvider(),
            new TcpSessionOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Mode = TcpSessionMode.Client,
                SessionId = "di-tcp-e2e-client",
                Resiliency = TestResiliencyOptions.FastReconnect(),
            });

        await using var clientProvider = clientServices.BuildServiceProvider();
        var clientBuilder = clientProvider.GetRequiredService<IMessagingSessionBuilder>();

        await Task.Delay(250);

        var alertEndpoint = serverBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestAlerts);
        var clientAlertEndpoint = clientBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestAlerts);

        var receivedTask = clientBuilder.MessagingService
            .GetMessageStream(clientAlertEndpoint)
            .SelectMany(stream => stream.Messages)
            .Select(message => clientBuilder.Serializer.Deserialize<TestAlertDto>(message.Payload))
            .FirstWithinAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(150);

        var alert = new TestAlertDto { MessageId = 7, Message = "DI", ServerName = "di-srv" };
        var payload = serverBuilder.Serializer.Serialize(alert);
        var message = serverBuilder.MessageFactory.Create(payload, TimeSpan.Zero, false);
        serverBuilder.MessagingService.Send(message, alertEndpoint);

        var received = await receivedTask;
        Assert.Equal(7, received.MessageId);
        Assert.Equal("DI", received.Message);
    }
}
