using System;

namespace Dcs.Messaging
{
    public interface IMessageFactory
    {
        IMessage Create(byte[] payload, TimeSpan timeToLive, bool isPersistent);
    }
}
