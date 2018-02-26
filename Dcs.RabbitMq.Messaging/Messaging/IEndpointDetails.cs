using System;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IEndpointDetails : IEquatable<IEndpointDetails>
    {
        string Address { get; }
    }
}
