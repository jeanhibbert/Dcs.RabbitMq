using Dcs.Messaging;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Tcp;

namespace Dcs.Messaging.Common
{
    public sealed class TcpMessagingSessionBuilder : MessagingSessionBuilderBase
    {
        public TcpMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions)
            : this(sessionName, endpointDetailsProvider, sessionOptions, retryPolicy: null, clock: null)
        {
        }

        public TcpMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions,
            IRetryPolicy retryPolicy,
            IClock clock)
            : this(
                sessionName,
                endpointDetailsProvider,
                sessionOptions,
                new TcpMessagingSession(sessionOptions, retryPolicy, clock))
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
            Session = session;
        }

        /// <summary>
        /// Direct access to the underlying TCP session for resiliency-aware
        /// callers (e.g. observing <see cref="IConnectionStateObserver.StateChanged"/>).
        /// </summary>
        public TcpMessagingSession Session { get; }
    }
}

