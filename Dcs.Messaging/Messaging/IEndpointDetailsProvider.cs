using System.Collections.Generic;

namespace Dcs.Messaging
{
    public interface IEndpointDetailsProvider
    {
        IEndpointDetails GetEndpointDetails(string endpointKey);
        bool TryGetEndpointDetails(string endpointKey, out IEndpointDetails endpointDetails);

        IDictionary<string, IEndpointDetails> GetEndpoints();
    }
}
