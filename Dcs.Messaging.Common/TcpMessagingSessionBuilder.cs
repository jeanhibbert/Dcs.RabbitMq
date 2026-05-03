using Dcs.Messaging;
using Dcs.Messaging.Tcp;

namespace Dcs.Messaging.Common
{
    public sealed class TcpMessagingSessionBuilder : MessagingSessionBuilderBase
    {
        public TcpMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions)
            : this(sessionName, endpointDetailsProvider, sessionOptions, new TcpMessagingSession(sessionOptions))
        {
        }

        private TcpMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions,
            TcpMessagingSession session)
            : base(
                sessionName,
                endpointDetailsProvider,
                sessionOptions.SessionId,
                session,
                new TcpEndpointProvider(session))
        {
        }
    }
}
