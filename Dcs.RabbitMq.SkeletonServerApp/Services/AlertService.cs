using Dcs.RabbitMq.Common;
using Dcs.RabbitMq.Common.Dto;
using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dcs.RabbitMq.SkeletonServerApp.Services
{
    public class AlertService : IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IMessageFactory _messageFactory;
        private readonly IEndpointDetailsFactory _endpointDetailsFactory;
        private readonly IBinarySerializer _serializer;
        private IDisposable _subscription = null;
        private readonly string _serverId;

        public AlertService(string serverId, IRabbitMqSessionBuilder sessionBuilder)
        {
            _serverId = serverId;
           // _messagingService = sessionBuilder.MessagingService;
            _messageFactory = sessionBuilder.MessageFactory;
            _serializer = sessionBuilder.Serializer;
            _endpointDetailsFactory = sessionBuilder.EndpointDetailsFactory;

            _endpoint = _endpointDetailsFactory.Create(_channelName, EndpointType.Default);
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

        IEndpointDetails _endpoint;

        private string _channelName = "Test/Alerts";

        private void HandleSend(int number)
        {
            var alertDto = new TestAlertDto { Message = number.ToString(), ServerName = _serverId };
            var serialized = _serializer.Serialize(alertDto);
            IMessage message = _messageFactory.Create(serialized, TimeSpan.Zero, false);
            _messagingService.Send(message, _endpoint);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_subscription != null)
                _subscription.Dispose();
        }

        #endregion
    }
}
