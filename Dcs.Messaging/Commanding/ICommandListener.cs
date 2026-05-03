using Dcs.Messaging;
using Dcs.Messaging.ServiceModel;
using System;

namespace Dcs.Messaging.Commanding
{
    public interface ICommandListener
    {
        IObservable<CommandWrapper<T>> GetCommandStream<T>(IEndpointDetails endpointDetails);
    }
}
