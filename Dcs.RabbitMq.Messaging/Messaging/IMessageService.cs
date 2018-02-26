using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IMessagingService
    {
        IObservable<Unit> Send(
            IMessage message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null);

        IObservable<IMessageStream> GetMessageStream(
            IEndpointDetails endpointDetails);
    }

    public interface IMessagingService<T>
    {
        IObservable<Unit> Send(
            T message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null,
            TimeSpan? timeToLive = null,
            bool isPersistent = false);

        IObservable<IMessageStream<T>> GetMessageStream(
            IEndpointDetails endpointDetails);
    }
}
