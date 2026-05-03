using Dcs.RabbitMq.Messaging.Grpc;
using Dcs.RabbitMq.Messaging.Messaging;

namespace Dcs.RabbitMq.Common
{
    public sealed class GrpcMessagingSessionBuilder : MessagingSessionBuilderBase
    {
        public GrpcMessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            GrpcSessionOptions sessionOptions)
            : this(sessionName, endpointDetailsProvider, sessionOptions, new GrpcMessagingSession(sessionOptions))
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
        }
    }
}
