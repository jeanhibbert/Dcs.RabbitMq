using Dcs.Messaging.Serialization;

namespace Dcs.Messaging
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