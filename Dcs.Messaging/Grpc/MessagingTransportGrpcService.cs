using Dcs.RabbitMq.Messaging.Transport;
using ProtoBuf.Grpc;
using System.Collections.Generic;

namespace Dcs.RabbitMq.Messaging.Grpc
{
    public sealed class MessagingTransportGrpcService : IMessagingTransportService
    {
        private readonly GrpcMessagingSession _session;

        public MessagingTransportGrpcService(GrpcMessagingSession session)
        {
            _session = session;
        }

        public IAsyncEnumerable<TransportEnvelope> Exchange(IAsyncEnumerable<TransportEnvelope> incoming, CallContext context = default)
        {
            return _session.AttachConnection(incoming, context);
        }
    }
}
