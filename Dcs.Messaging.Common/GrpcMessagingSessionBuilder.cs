using Dcs.Messaging;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.Common
{
    public sealed class GrpcMessagingSessionBuilder : MessagingSessionBuilderBase
    {
        public GrpcMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            GrpcSessionOptions sessionOptions)
            : this(sessionName, endpointDetailsProvider, sessionOptions, retryPolicy: null, clock: null)
        {
        }

        public GrpcMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            GrpcSessionOptions sessionOptions,
            IRetryPolicy retryPolicy,
            IClock clock)
            : this(
                sessionName,
                endpointDetailsProvider,
                sessionOptions,
                new GrpcMessagingSession(sessionOptions, retryPolicy, clock))
        {
        }

        private GrpcMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            GrpcSessionOptions sessionOptions,
            GrpcMessagingSession session)
            : base(
                sessionName,
                endpointDetailsProvider,
                sessionOptions.SessionId,
                session,
                new GrpcEndpointProvider(session))
        {
            Session = session;
        }

        /// <summary>
        /// Direct access to the underlying gRPC session for resiliency-aware
        /// callers (e.g. observing <see cref="IConnectionStateObserver.StateChanged"/>).
        /// </summary>
        public GrpcMessagingSession Session { get; }
    }
}

