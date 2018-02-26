namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IEndpointProvider
    {
        IEndpoint GetEndpoint(IEndpointDetails endpointDetails, bool createIfMissing = false);
    }
}
