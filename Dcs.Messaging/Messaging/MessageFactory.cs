using System;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public sealed class MessageFactory : IMessageFactory
    {
        private readonly string _senderSessionId;

        public MessageFactory(string senderSessionId)
        {
            _senderSessionId = senderSessionId ?? string.Empty;
        }

        public IMessage Create(byte[] payload, TimeSpan timeToLive, bool isPersistent)
        {
            var properties = new MessageProperties();
            properties.Set(TransportPropertyKeys.TimeToLiveMs, (int)timeToLive.TotalMilliseconds);
            properties.Set(TransportPropertyKeys.IsPersistent, isPersistent);
            return new Message(_senderSessionId, string.Empty, properties, payload);
        }
    }
}