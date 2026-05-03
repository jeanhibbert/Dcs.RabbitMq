using Dcs.Messaging.Common;
using Dcs.Messaging.Grpc;

namespace Dcs.Messaging.UnitTests.Builders;

public class GrpcMessagingSessionBuilderTests : IDisposable
{
    private readonly GrpcMessagingSessionBuilder _builder;

    public GrpcMessagingSessionBuilderTests()
    {
        var options = GrpcSessionOptions.CreateServer("http://127.0.0.1:15051", "grpc-test-session");
        _builder = new GrpcMessagingSessionBuilder("GrpcTestApp", new TestEndpointDetailsProvider(), options);
    }

    public void Dispose()
    {
        _builder.MessagingTransport?.Dispose();
    }

    [Fact]
    public void Constructor_SetsMessagingTransportToGrpcSession()
    {
        Assert.NotNull(_builder.MessagingTransport);
        Assert.IsType<GrpcMessagingSession>(_builder.MessagingTransport);
    }

    [Fact]
    public void Constructor_SetsMessagingService()
    {
        Assert.NotNull(_builder.MessagingService);
    }

    [Fact]
    public void Constructor_SetsSerializer()
    {
        Assert.NotNull(_builder.Serializer);
    }

    [Fact]
    public void Constructor_SetsRequestResponder()
    {
        Assert.NotNull(_builder.RequestResponder);
    }

    [Fact]
    public void Constructor_SetsMessageFactory()
    {
        Assert.NotNull(_builder.MessageFactory);
    }

    [Fact]
    public void Constructor_SetsEndpointDetailsFactory()
    {
        Assert.NotNull(_builder.EndpointDetailsFactory);
    }

    [Fact]
    public void Constructor_SetsEndpointDetailsProvider()
    {
        Assert.NotNull(_builder.EndpointDetailsProvider);
        Assert.IsType<TestEndpointDetailsProvider>(_builder.EndpointDetailsProvider);
    }

    [Fact]
    public void ImplementsIMessagingSessionBuilder()
    {
        Assert.IsAssignableFrom<IMessagingSessionBuilder>(_builder);
    }
}
