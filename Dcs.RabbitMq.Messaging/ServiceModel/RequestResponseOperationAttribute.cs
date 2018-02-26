using System;

namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequestResponseOperationAttribute : OperationAttribute
    {
        public string RequestEndpointKey { get; private set; }
        public string ResponseEndpointKey { get; private set; }

        public RequestResponseOperationAttribute(
            string requestEndpointKey,
            string responseEndpointKey)
        {
            RequestEndpointKey = requestEndpointKey;
            ResponseEndpointKey = responseEndpointKey;
        }
    }
}
