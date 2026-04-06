using Dcs.RabbitMq.Common;
using Dcs.RabbitMq.Messaging.Grpc;
using Dcs.RabbitMq.SkeletonServerApp.Services;
using System;

namespace Dcs.RabbitMq.SkeletonServerApp.Grpc
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var endpointDetailsProvider = new TestEndpointDetailsProvider();
            var serverId = Guid.NewGuid().ToString();
            var sessionOptions = GrpcSessionOptions.CreateServer("http://127.0.0.1:5050", serverId);
            var sessionBuilder = new GrpcMessagingSessionBuilder("ServerApp", endpointDetailsProvider, sessionOptions);

            using (sessionBuilder.MessagingTransport)
            {
                using (var alertService = new AlertService(serverId, sessionBuilder))
                using (var pricingService = new PricingService(sessionBuilder))
                {
                    pricingService.Start();
                    alertService.Start();
                    Console.WriteLine("[gRPC] Server listening on http://127.0.0.1:5050 with session {0}", serverId);
                    Console.WriteLine("Press ENTER to stop service...");
                    Console.ReadLine();
                }
            }
        }
    }
}
