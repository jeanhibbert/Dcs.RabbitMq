using Dcs.RabbitMq.Messaging.Extensions;
using Dcs.RabbitMq.Messaging.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public abstract class MessagingServiceBase : IMessagingService
    {
        private readonly IEndpointProvider _endpointProvider;

        protected MessagingServiceBase(IEndpointProvider endpointProvider)
        {
            _endpointProvider = endpointProvider;
        }

        public IObservable<Unit> Send(
            IMessage message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null)
        {
            return Extensions.ObservableExtensions.ReturnAsync(() =>
            {
                var endpoint = _endpointProvider.GetEndpoint(endpointDetails);
                endpoint.Send(message, targetSessionId);
                return new Unit();
            });
        }

        public IObservable<IMessageStream> GetMessageStream(IEndpointDetails endpointDetails)
        {
            return Extensions.ObservableExtensions.ReturnAsync(() =>
            {
                return _endpointProvider.GetEndpoint(endpointDetails).MessageStream;
            });
        }
    }

    public abstract class MessagingServiceBase<T> : IMessagingService<T>
    {
        public static readonly TimeSpan DefaultTimeToLive = TimeSpan.Zero;

        private readonly IMessagingService _messagingService;
        private readonly IMessageFactory _messageFactory;
        private readonly IBinarySerializer _serializer;

        protected MessagingServiceBase(
            IMessagingService messagingService,
            IMessageFactory messageFactory,
            IBinarySerializer serializer)
        {
            _messagingService = messagingService;
            _messageFactory = messageFactory;
            _serializer = serializer;
        }

        public IObservable<Unit> Send(
            T message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null,
            TimeSpan? timeToLive = null,
            bool isPersistent = false)
        {
            var payload = _serializer.Serialize(message);
            var outMessage = _messageFactory.Create(
                payload,
                timeToLive ?? DefaultTimeToLive,
                isPersistent);
            return _messagingService.Send(outMessage, endpointDetails, targetSessionId);
        }

        public IObservable<IMessageStream<T>> GetMessageStream(IEndpointDetails endpointDetails)
        {
            return _messagingService.GetMessageStream(endpointDetails)
                .Take(1)
                .Select(messageStream =>
                {
                    var messages = messageStream.Messages
                        .Select(message => message.Payload)
                        .Select(_serializer.Deserialize<T>);
                    return new MessageStream<T>(messages).As<IMessageStream<T>>();
                });
        }
    }
}
