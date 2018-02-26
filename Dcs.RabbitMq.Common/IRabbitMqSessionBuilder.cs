using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;

namespace Dcs.RabbitMq.Common
{
    public interface IRabbitMqSessionBuilder
    {
        IEndpointDetailsFactory EndpointDetailsFactory { get; }
        IEndpointDetailsProvider EndpointDetailsProvider { get; }
        //IRabbitMqSettingsProvider RabbitMqSettingsProvider { get; }
        //IEndpointProvider EndpointProvider { get; }
        IMessageFactory MessageFactory { get; }
        //RabbitMqMessagingService MessagingService { get; }
        //RabbitMqMessagingSession MessagingSession { get; }
        IBinarySerializer Serializer { get; }
    }
}
