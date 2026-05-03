namespace Dcs.Messaging
{
    public interface IEndpointProvider
    {
        IEndpoint GetEndpoint(IEndpointDetails endpointDetails, bool createIfMissing = false);
    }
}
