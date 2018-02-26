using System;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IMessageStream
    {
        IObservable<IMessage> Messages { get; }
    }

    public interface IMessageStream<T>
    {
        IObservable<T> Messages { get; }
    }
}