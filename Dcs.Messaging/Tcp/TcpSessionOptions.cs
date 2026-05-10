using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.Tcp
{
    public sealed class TcpSessionOptions
    {
        public TcpSessionOptions()
        {
        }

        public string Host { get; set; }

        public TcpSessionMode Mode { get; set; }

        public int Port { get; set; }

        public string SessionId { get; set; }

        /// <summary>
        /// Resiliency tuning. Defaults to <see cref="ResiliencyOptions.Default"/>.
        /// </summary>
        public ResiliencyOptions Resiliency { get; set; } = ResiliencyOptions.Default;

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
