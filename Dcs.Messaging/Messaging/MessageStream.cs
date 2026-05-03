using System;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public sealed class MessageStream : IMessageStream
    {
        private readonly IObservable<IMessage> _messages;

        public MessageStream(IObservable<IMessage> messages)
        {
            _messages = messages;
        }

        public IObservable<IMessage> Messages
        {
            get { return _messages; }
        }
    }

    public sealed class MessageStream<T> : IMessageStream<T>
    {
        private readonly IObservable<T> _messages;

        public MessageStream(IObservable<T> messages)
        {
            _messages = messages;
        }

        public IObservable<T> Messages
        {
            get { return _messages; }
        }
    }
}