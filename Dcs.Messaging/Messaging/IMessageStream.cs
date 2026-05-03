using System;

namespace Dcs.Messaging
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