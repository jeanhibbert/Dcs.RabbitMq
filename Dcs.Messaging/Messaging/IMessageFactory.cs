using System;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IMessageFactory
    {
        IMessage Create(byte[] payload, TimeSpan timeToLive, bool isPersistent);
    }
}
