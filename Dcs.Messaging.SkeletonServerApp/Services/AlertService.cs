using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging;
using Dcs.Messaging.Serialization;
using System;
using System.Reactive.Linq;

namespace Dcs.Messaging.SkeletonServerApp.Services
{
    public sealed class AlertService : IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IMessageFactory _messageFactory;
        private readonly IEndpointDetails _endpoint;
        private readonly IBinarySerializer _serializer;
        private IDisposable _subscription;
        private readonly string _serverId;

        public AlertService(string serverId, IMessagingSessionBuilder sessionBuilder)
        {
            _serverId = serverId;
            _messagingService = sessionBuilder.MessagingService;
            _messageFactory = sessionBuilder.MessageFactory;
            _serializer = sessionBuilder.Serializer;
            _endpoint = sessionBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestAlerts);
        }

        public void Start()
        {
            int noOfMessagesToSend = 1000000;
            IObservable<int> source = Observable.Generate(0, i => i < noOfMessagesToSend, i => i + 1, i => i, i => TimeSpan.FromSeconds(1));
            _subscription = source.Subscribe(
                x =>
                {
                    HandleSend(x);
                    Console.WriteLine("Sent message: {0} - from Server : {1}", x, _serverId);
                },
                ex =>
                {
                    Console.WriteLine("Error sending message: {0}", ex.Message);
                },
                () => Console.WriteLine("Stopped sending messages"));

        }

        private void HandleSend(int number)
        {
            var alertDto = new TestAlertDto { MessageId = number, Message = number.ToString(), ServerName = _serverId };
            var serialized = _serializer.Serialize(alertDto);
            IMessage message = _messageFactory.Create(serialized, TimeSpan.Zero, false);
            _messagingService.Send(message, _endpoint);
        }

        #region IDisposable Members

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        #endregion
    }
}
