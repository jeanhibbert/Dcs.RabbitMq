using System;
using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;

namespace Dcs.RabbitMq.Common
{
    public class RabbitMqSessionBuilder : IRabbitMqSessionBuilder
    {

        private readonly string _sessionFullName;
        private string v;
        private TestEndpointDetailsProvider endpointDetailsProvider;
        private object p;

        public RabbitMqSessionBuilder(string v, TestEndpointDetailsProvider endpointDetailsProvider, object p)
        {
            this.v = v;
            this.endpointDetailsProvider = endpointDetailsProvider;
            this.p = p;
        }

        public string SessionFullName
        {
            get { return _sessionFullName; }
        }

        public RabbitMqMessagingSession MessagingSession
        {
            get { return new RabbitMqMessagingSession(); } // COMPLETE
        }

        public IEndpointDetailsFactory EndpointDetailsFactory => throw new NotImplementedException();

        public IEndpointDetailsProvider EndpointDetailsProvider => throw new NotImplementedException();

        public IMessageFactory MessageFactory => throw new NotImplementedException();

        public IBinarySerializer Serializer => throw new NotImplementedException();
    }
}
