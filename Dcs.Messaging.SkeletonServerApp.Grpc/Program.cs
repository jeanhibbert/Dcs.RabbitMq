using Dcs.Messaging.Common;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.SkeletonServerApp.Services;
using System;

namespace Dcs.Messaging.SkeletonServerApp.Grpc
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
