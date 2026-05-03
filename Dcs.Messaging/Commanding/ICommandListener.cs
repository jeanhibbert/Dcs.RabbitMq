using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.ServiceModel;
using System;

namespace Dcs.RabbitMq.Messaging.Commanding
{
    public interface ICommandListener
    {
        IObservable<CommandWrapper<T>> GetCommandStream<T>(IEndpointDetails endpointDetails);
    }
}
