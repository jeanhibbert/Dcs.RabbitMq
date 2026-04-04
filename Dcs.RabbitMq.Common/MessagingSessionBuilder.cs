using Dcs.RabbitMq.Messaging;
using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.RequestResponse;
using Dcs.RabbitMq.Messaging.Serialization;
using Dcs.RabbitMq.Messaging.Tcp;

namespace Dcs.RabbitMq.Common
{
    public sealed class MessagingSessionBuilder : IMessagingSessionBuilder
    {
        public MessagingSessionBuilder(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions)
        {
            EndpointDetailsProvider = endpointDetailsProvider;
            EndpointDetailsFactory = new MessageEndpointDetailsFactory();
            MessageFactory = new MessageFactory(sessionOptions.SessionId ?? sessionName);
            Serializer = new ProtobufNetBinarySerializer();
            MessagingSession = new TcpMessagingSession(sessionOptions);

            var endpointProvider = new TcpEndpointProvider(MessagingSession);
            MessagingService = new TcpMessagingService(endpointProvider);
            RequestResponder = new TcpRequestResponder(MessagingService, MessageFactory, Serializer);
        }

        public IEndpointDetailsFactory EndpointDetailsFactory { get; }

        public IEndpointDetailsProvider EndpointDetailsProvider { get; }

        public IMessageFactory MessageFactory { get; }

        public IMessagingService MessagingService { get; }

        public IRequestResponder RequestResponder { get; }

        public IBinarySerializer Serializer { get; }

        public TcpMessagingSession MessagingSession { get; }
    }
}