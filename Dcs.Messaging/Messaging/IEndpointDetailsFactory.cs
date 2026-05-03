namespace Dcs.Messaging
{
    public interface IEndpointDetailsFactory
    {
        IEndpointDetails Create(string endpointName, EndpointType endpointType);
    }
}
