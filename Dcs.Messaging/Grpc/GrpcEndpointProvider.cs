using Dcs.RabbitMq.Messaging.Messaging;
using System;
using System.Collections.Concurrent;

namespace Dcs.RabbitMq.Messaging.Grpc
{
    public sealed class GrpcEndpointProvider : IEndpointProvider
    {
        private readonly ConcurrentDictionary<string, IEndpoint> _endpoints;
        private readonly GrpcMessagingSession _session;

        public GrpcEndpointProvider(GrpcMessagingSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _endpoints = new ConcurrentDictionary<string, IEndpoint>(StringComparer.Ordinal);
        }

        public IEndpoint GetEndpoint(IEndpointDetails endpointDetails, bool createIfMissing = false)
        {
            return _endpoints.GetOrAdd(
                endpointDetails.Address,
                _ => new GrpcEndpoint(_session, endpointDetails));
        }
    }
}
