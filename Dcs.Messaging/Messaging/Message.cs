using System;

namespace Dcs.Messaging
{
    public sealed class Message : IMessage
    {
        public Message(
            string senderSessionId,
            string tag,
            IMessageProperties properties,
            byte[] payload)
        {
            SenderSessionId = senderSessionId ?? string.Empty;
            Tag = tag ?? string.Empty;
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            Payload = payload ?? Array.Empty<byte>();
        }

        public string SenderSessionId { get; }

        public string Tag { get; }

        public IMessageProperties Properties { get; }

        public byte[] Payload { get; }
    }
}