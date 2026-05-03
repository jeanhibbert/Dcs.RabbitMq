using System.Collections.Generic;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IEndpointDetailsProvider
    {
        IEndpointDetails GetEndpointDetails(string endpointKey);
        bool TryGetEndpointDetails(string endpointKey, out IEndpointDetails endpointDetails);

        IDictionary<string, IEndpointDetails> GetEndpoints();
    }
}
