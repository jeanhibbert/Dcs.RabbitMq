using Dcs.RabbitMq.Messaging.Messaging;
using System;
using System.Collections.Concurrent;

namespace Dcs.RabbitMq.Messaging.Tcp
{
    public sealed class TcpEndpointProvider : IEndpointProvider
    {
        private readonly ConcurrentDictionary<string, IEndpoint> _endpoints;
        private readonly TcpMessagingSession _session;

        public TcpEndpointProvider(TcpMessagingSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _endpoints = new ConcurrentDictionary<string, IEndpoint>(StringComparer.Ordinal);
        }

        public IEndpoint GetEndpoint(IEndpointDetails endpointDetails, bool createIfMissing = false)
        {
            return _endpoints.GetOrAdd(
                endpointDetails.Address,
                _ => new TcpEndpoint(_session, endpointDetails));
        }
    }
}