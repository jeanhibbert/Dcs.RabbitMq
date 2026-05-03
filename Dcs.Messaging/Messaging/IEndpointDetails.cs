using System;

namespace Dcs.Messaging
{
    public interface IEndpointDetails : IEquatable<IEndpointDetails>
    {
        string Address { get; }
    }
}
