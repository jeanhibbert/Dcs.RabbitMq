using Dcs.RabbitMq.Common;
using Dcs.RabbitMq.SkeletonServerApp.Services;
using System;

namespace Dcs.RabbitMq.SkeletonServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestEndpointDetailsProvider endpointDetailsProvider = new TestEndpointDetailsProvider();
            //TestRabbitMqSettingsProvider settingsProvider = new TestRabbitMqSettingsProvider();
            RabbitMqSessionBuilder sessionBuilder = new RabbitMqSessionBuilder("ServerApp", endpointDetailsProvider, null);
            string serverId = Guid.NewGuid().ToString();

            using (sessionBuilder.MessagingSession)
            {
                using (AlertService asm = new AlertService(serverId, sessionBuilder))
                {
                    asm.Start();
                    Console.WriteLine("Press ENTER to stop service...");
                    Console.ReadLine();
                }
            }
        }
    }
}
