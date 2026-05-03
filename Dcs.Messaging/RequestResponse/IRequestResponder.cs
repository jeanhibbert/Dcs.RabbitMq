using Dcs.Messaging;
using System;

namespace Dcs.Messaging.RequestResponse
{
    public interface IRequestResponder
    {
        IObservable<IRespondableRequest<TRequest, TResponse>>
            GetRespondableRequestStream<TRequest, TResponse>(
                IEndpointDetails requestEndpointDetails,
                IEndpointDetails responseEndpointDetails);

        void InitializeEndpoint(
            IEndpointDetails requestEndpointDetails,
            IEndpointDetails responseEndpointDetails);
    }
}
