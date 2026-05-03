using System;

namespace Dcs.RabbitMq.Messaging.RequestResponse
{
    public interface IRespondableRequest<out TRequest, in TResponse>
    {
        string SenderSessionId { get; }
        TRequest Request { get; }
        void Respond(TResponse response);
        void Respond(Exception exception);
    }
}
