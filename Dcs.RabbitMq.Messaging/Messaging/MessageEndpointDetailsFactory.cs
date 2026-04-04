namespace Dcs.RabbitMq.Messaging.Messaging
{
    public sealed class MessageEndpointDetailsFactory : IEndpointDetailsFactory
    {
        public IEndpointDetails Create(string endpointName, EndpointType endpointType)
        {
            return new MessageEndpointDetails(endpointName, endpointType);
        }
    }
}