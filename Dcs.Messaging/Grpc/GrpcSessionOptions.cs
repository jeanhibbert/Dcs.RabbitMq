using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.Grpc
{
    /// <summary>
    /// Options for a gRPC messaging session. Use an HTTP(S) base URL such as <c>http://127.0.0.1:5050</c>.
    /// For local development without TLS, use HTTP/2 cleartext (h2c): listen with HTTP/2 only and on the client
    /// enable unencrypted HTTP/2 (see <see cref="EnableHttp2Unencrypted"/> and <see cref="GrpcMessagingSession"/>).
    /// </summary>
    public sealed class GrpcSessionOptions
    {
        public GrpcSessionOptions()
        {
        }

        /// <summary>
        /// Base URL for Kestrel (server) or gRPC channel (client), e.g. <c>http://127.0.0.1:5050</c>.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// When true (default), the client sets <c>AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true)</c>
        /// so gRPC over <c>http://</c> (h2c) works. Not used on the server.
        /// </summary>
        public bool EnableHttp2Unencrypted { get; set; } = true;

        public GrpcSessionMode Mode { get; set; }

        public string SessionId { get; set; }

        /// <summary>
        /// Resiliency tuning. Defaults to <see cref="ResiliencyOptions.Default"/>.
        /// </summary>
        public ResiliencyOptions Resiliency { get; set; } = ResiliencyOptions.Default;

        public static GrpcSessionOptions CreateClient(string baseUrl, string sessionId)
        {
            return new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Client,
                SessionId = sessionId
            };
        }

        public static GrpcSessionOptions CreateServer(string baseUrl, string sessionId)
        {
            return new GrpcSessionOptions
            {
                BaseUrl = baseUrl,
                Mode = GrpcSessionMode.Server,
                SessionId = sessionId
            };
        }
    }
}
