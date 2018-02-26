namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IEndpointDetailsFactory
    {
        IEndpointDetails Create(string endpointName, EndpointType endpointType);
    }
}
