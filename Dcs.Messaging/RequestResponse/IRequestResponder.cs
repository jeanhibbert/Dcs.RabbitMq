using Dcs.RabbitMq.Messaging.Messaging;
using System;

namespace Dcs.RabbitMq.Messaging.RequestResponse
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
