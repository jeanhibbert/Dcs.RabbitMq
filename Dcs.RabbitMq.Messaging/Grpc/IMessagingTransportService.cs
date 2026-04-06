using Dcs.RabbitMq.Messaging.Transport;
using ProtoBuf.Grpc;
using System.Collections.Generic;
using System.ServiceModel;

namespace Dcs.RabbitMq.Messaging.Grpc
{
    [ServiceContract]
    public interface IMessagingTransportService
    {
        [OperationContract]
        IAsyncEnumerable<TransportEnvelope> Exchange(IAsyncEnumerable<TransportEnvelope> incoming, CallContext context = default);
    }
}
