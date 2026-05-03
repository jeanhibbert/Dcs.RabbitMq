using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Dcs.RabbitMq.Messaging.RequestResponse
{
    public sealed class TcpRequestResponder : IRequestResponder
    {
        private readonly IMessageFactory _messageFactory;
        private readonly IMessagingService _messagingService;
        private readonly IBinarySerializer _serializer;

        public TcpRequestResponder(
            IMessagingService messagingService,
            IMessageFactory messageFactory,
            IBinarySerializer serializer)
        {
            _messagingService = messagingService;
            _messageFactory = messageFactory;
            _serializer = serializer;
        }

        public IObservable<IRespondableRequest<TRequest, TResponse>> GetRespondableRequestStream<TRequest, TResponse>(
            IEndpointDetails requestEndpointDetails,
            IEndpointDetails responseEndpointDetails)
        {
            return _messagingService
                .GetMessageStream(requestEndpointDetails)
                .SelectMany(stream =>
                    stream.Messages.Select(message =>
                    {
                        var correlationId = message.Properties.GetString(TransportPropertyKeys.CorrelationId) ?? Guid.NewGuid().ToString("N");
                        var request = _serializer.Deserialize<TRequest>(message.Payload);
                        return (IRespondableRequest<TRequest, TResponse>)new RespondableRequest<TRequest, TResponse>(
                            message.SenderSessionId,
                            request,
                            correlationId,
                            responseEndpointDetails,
                            _messagingService,
                            _messageFactory,
                            _serializer);
                    }));
        }

        public void InitializeEndpoint(IEndpointDetails requestEndpointDetails, IEndpointDetails responseEndpointDetails)
        {
        }
    }
}