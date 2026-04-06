using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.RequestResponse;
using Dcs.RabbitMq.Messaging.Serialization;
using System;

namespace Dcs.RabbitMq.Common
{
    public interface IMessagingSessionBuilder
    {
        IEndpointDetailsFactory EndpointDetailsFactory { get; }
        IEndpointDetailsProvider EndpointDetailsProvider { get; }
        IMessageFactory MessageFactory { get; }
        IMessagingService MessagingService { get; }
        IDisposable MessagingTransport { get; }
        IRequestResponder RequestResponder { get; }
        IBinarySerializer Serializer { get; }
    }
}
