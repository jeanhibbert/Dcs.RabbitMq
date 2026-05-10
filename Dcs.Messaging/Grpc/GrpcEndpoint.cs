using Dcs.Messaging;
using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.Grpc
{
    internal sealed class GrpcEndpoint : IEndpoint
    {
        private readonly IEndpointDetails _endpointDetails;
        private readonly GrpcMessagingSession _session;

        public GrpcEndpoint(GrpcMessagingSession session, IEndpointDetails endpointDetails)
        {
            _session = session;
            _endpointDetails = endpointDetails;
        }

        public IMessageStream MessageStream => _session.GetMessageStream(_endpointDetails);

        public void Send(IMessage message, string sessionId = null)
        {
            _session.Send(_endpointDetails, message, sessionId);
        }

        public SendResult TrySend(IMessage message, string sessionId = null)
        {
            return _session.TrySend(_endpointDetails, message, sessionId);
        }
    }
}

