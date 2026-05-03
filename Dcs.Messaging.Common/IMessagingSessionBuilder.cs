using Dcs.Messaging;
using Dcs.Messaging.RequestResponse;
using Dcs.Messaging.Serialization;
using System;

namespace Dcs.Messaging.Common
{
    public interface IMessagingSessionBuilder
    {
        IEndpointDetailsFactory EndpointDetailsFactory { get; }
        IEndpointDetailsProvider EndpointDetailsProvider { get; }
        IMessageFactory MessageFactory { get; }
        IMessagingService MessagingService { get; }
        IDisposable MessagingTransport { get; }
        IRequestResponder RequestResponder { get; }
        IBinarySerializer Serializer { get; }
    }
}
