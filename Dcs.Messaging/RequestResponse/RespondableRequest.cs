using Dcs.Messaging;
using Dcs.Messaging.Serialization;
using System;

namespace Dcs.Messaging.RequestResponse
{
    internal sealed class RespondableRequest<TRequest, TResponse> : IRespondableRequest<TRequest, TResponse>
    {
        private readonly string _correlationId;
        private readonly IMessageFactory _messageFactory;
        private readonly IMessagingService _messagingService;
        private readonly IEndpointDetails _responseEndpoint;
        private readonly IBinarySerializer _serializer;

        public RespondableRequest(
            string senderSessionId,
            TRequest request,
            string correlationId,
            IEndpointDetails responseEndpoint,
            IMessagingService messagingService,
            IMessageFactory messageFactory,
            IBinarySerializer serializer)
        {
            SenderSessionId = senderSessionId;
            Request = request;
            _correlationId = correlationId;
            _responseEndpoint = responseEndpoint;
            _messagingService = messagingService;
            _messageFactory = messageFactory;
            _serializer = serializer;
        }

        public string SenderSessionId { get; }

        public TRequest Request { get; }

        public void Respond(TResponse response)
        {
            var payload = _serializer.Serialize(response);
            var message = _messageFactory.Create(payload, TimeSpan.Zero, false);
            message.Properties.Set(TransportPropertyKeys.CorrelationId, _correlationId);
            message.Properties.Set(TransportPropertyKeys.MessageKind, MessageKinds.Response);
            _messagingService.Send(message, _responseEndpoint, SenderSessionId);
        }

        public void Respond(Exception exception)
        {
            var message = _messageFactory.Create(Array.Empty<byte>(), TimeSpan.Zero, false);
            message.Properties.Set(TransportPropertyKeys.CorrelationId, _correlationId);
            message.Properties.Set(TransportPropertyKeys.ErrorMessage, exception.Message);
            message.Properties.Set(TransportPropertyKeys.IsError, true);
            message.Properties.Set(TransportPropertyKeys.MessageKind, MessageKinds.Response);
            _messagingService.Send(message, _responseEndpoint, SenderSessionId);
        }
    }
}