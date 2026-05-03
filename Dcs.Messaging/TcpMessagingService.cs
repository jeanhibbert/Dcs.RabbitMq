using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;

namespace Dcs.RabbitMq.Messaging
{
    public sealed class TcpMessagingService : MessagingServiceBase
    {
        public TcpMessagingService(IEndpointProvider endpointProvider)
            : base(endpointProvider)
        {
        }
    }

    public sealed class TcpMessagingService<T> : MessagingServiceBase<T>
    {
        public TcpMessagingService(
            IMessagingService messagingService,
            IMessageFactory messageFactory,
            IBinarySerializer serializer)
            : base(messagingService, messageFactory, serializer)
        {
        }
    }
}