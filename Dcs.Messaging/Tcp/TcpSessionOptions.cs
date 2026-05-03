namespace Dcs.RabbitMq.Messaging.Tcp
{
    public sealed class TcpSessionOptions
    {
        private TcpSessionOptions()
        {
        }

        public string Host { get; set; }

        public TcpSessionMode Mode { get; set; }

        public int Port { get; set; }

        public string SessionId { get; set; }

        public static TcpSessionOptions CreateClient(string host, int port, string sessionId)
        {
            return new TcpSessionOptions
            {
                Host = host,
                Mode = TcpSessionMode.Client,
                Port = port,
                SessionId = sessionId
            };
        }

        public static TcpSessionOptions CreateServer(string host, int port, string sessionId)
        {
            return new TcpSessionOptions
            {
                Host = host,
                Mode = TcpSessionMode.Server,
                Port = port,
                SessionId = sessionId
            };
        }
    }
}