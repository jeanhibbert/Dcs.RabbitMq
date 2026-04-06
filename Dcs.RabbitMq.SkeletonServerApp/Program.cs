using Dcs.RabbitMq.Common;
using Dcs.RabbitMq.Messaging.Tcp;
using Dcs.RabbitMq.SkeletonServerApp.Services;
using System;

namespace Dcs.RabbitMq.SkeletonServerApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            TestEndpointDetailsProvider endpointDetailsProvider = new TestEndpointDetailsProvider();
            string serverId = Guid.NewGuid().ToString();
            var sessionOptions = TcpSessionOptions.CreateServer("127.0.0.1", 5050, serverId);
            var sessionBuilder = new TcpMessagingSessionBuilder("ServerApp", endpointDetailsProvider, sessionOptions);

            using (sessionBuilder.MessagingTransport)
            {
                using (AlertService alertService = new AlertService(serverId, sessionBuilder))
                using (PricingService pricingService = new PricingService(sessionBuilder))
                {
                    pricingService.Start();
                    alertService.Start();
                    Console.WriteLine("Server listening on 127.0.0.1:5050 with session {0}", serverId);
                    Console.WriteLine("Press ENTER to stop service...");
                    Console.ReadLine();
                }
            }
        }
    }
}
