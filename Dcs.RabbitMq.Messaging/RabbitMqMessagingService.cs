using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;


namespace Dcs.RabbitMq.Messaging
{
    public sealed class RabbitMqMessagingService : MessagingServiceBase
    {
        public RabbitMqMessagingService(IEndpointProvider endpointProvider)
            : base(endpointProvider)
        {
        }
    }

    public sealed class RabbitMqMessagingService<T> : MessagingServiceBase<T>
    {
        public RabbitMqMessagingService(
            IMessagingService messagingService,
            IMessageFactory messageFactory,
            IBinarySerializer serializer)
            : base(messagingService, messageFactory, serializer)
        {
        }
    }
}
