using Dcs.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dcs.Messaging.Transport
{
    internal static class TransportEnvelopeFactory
    {
        public static TransportEnvelope FromMessage(string endpointAddress, IMessage message, string targetSessionId)
        {
            var properties = message.Properties as MessageProperties;
            var propertyValues = new List<TransportPropertyValue>();

            if (properties != null)
            {
                foreach (var pair in properties.ToDictionary())
                {
                    propertyValues.Add(TransportPropertyValue.From(pair.Key, pair.Value));
                }
            }

            return new TransportEnvelope
            {
                EndpointAddress = endpointAddress,
                Payload = message.Payload ?? Array.Empty<byte>(),
                Properties = propertyValues,
                SenderSessionId = message.SenderSessionId,
                Tag = message.Tag,
                TargetSessionId = targetSessionId ?? string.Empty
            };
        }

        public static IMessage ToMessage(TransportEnvelope envelope)
        {
            var properties = new MessageProperties();
            foreach (var property in envelope.Properties ?? Enumerable.Empty<TransportPropertyValue>())
            {
                property.ApplyTo(properties);
            }

            return new Message(
                envelope.SenderSessionId ?? string.Empty,
                envelope.Tag ?? string.Empty,
                properties,
                envelope.Payload ?? Array.Empty<byte>());
        }
    }
}
