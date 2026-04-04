using Dcs.RabbitMq.Messaging.Messaging;

namespace Dcs.RabbitMq.Messaging.Tcp
{
    internal sealed class TcpEndpoint : IEndpoint
    {
        private readonly IEndpointDetails _endpointDetails;
        private readonly TcpMessagingSession _session;

        public TcpEndpoint(TcpMessagingSession session, IEndpointDetails endpointDetails)
        {
            _session = session;
            _endpointDetails = endpointDetails;
        }

        public IMessageStream MessageStream => _session.GetMessageStream(_endpointDetails);

        public void Send(IMessage message, string sessionId = null)
        {
            _session.Send(_endpointDetails, message, sessionId);
        }
    }
}