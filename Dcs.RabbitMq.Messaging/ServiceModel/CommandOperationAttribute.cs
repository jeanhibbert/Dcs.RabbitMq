using System;

namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CommandOperationAttribute : OperationAttribute
    {
        public string CommandEndpointKey { get; private set; }

        public CommandOperationAttribute(string commandEndpointKey)
        {
            CommandEndpointKey = commandEndpointKey;
        }
    }
}
