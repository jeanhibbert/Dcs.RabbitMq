using Dcs.RabbitMq.Messaging;
using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.RequestResponse;
using Dcs.RabbitMq.Messaging.Serialization;
using System;

namespace Dcs.RabbitMq.Common
{
    public abstract class MessagingSessionBuilderBase : IMessagingSessionBuilder
    {
        protected MessagingSessionBuilderBase(
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            string sessionId,
            IDisposable transport,
            IEndpointProvider endpointProvider)
        {
            EndpointDetailsProvider = endpointDetailsProvider;
            EndpointDetailsFactory = new MessageEndpointDetailsFactory();
            MessageFactory = new MessageFactory(sessionId ?? sessionName);
            Serializer = new ProtobufNetBinarySerializer();
            MessagingTransport = transport;

            MessagingService = new TcpMessagingService(endpointProvider);
            RequestResponder = new TcpRequestResponder(MessagingService, MessageFactory, Serializer);
        }

        public IEndpointDetailsFactory EndpointDetailsFactory { get; }

        public IEndpointDetailsProvider EndpointDetailsProvider { get; }

        public IMessageFactory MessageFactory { get; }

        public IMessagingService MessagingService { get; }

        public IDisposable MessagingTransport { get; }

        public IRequestResponder RequestResponder { get; }

        public IBinarySerializer Serializer { get; }
    }
}
