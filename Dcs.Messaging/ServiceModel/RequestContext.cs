namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    public sealed class RequestContext
    {
        private readonly string _senderSessionId;

        public RequestContext(string senderSessionId)
        {
            _senderSessionId = senderSessionId;
        }

        public string SenderSessionId
        {
            get { return _senderSessionId; }
        }

        public static RequestContext Current
        {
            get;
            internal set;
        }
    }
}
