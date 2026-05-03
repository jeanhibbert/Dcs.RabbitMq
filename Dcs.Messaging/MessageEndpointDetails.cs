using System;

namespace Dcs.Messaging
{
    public sealed class MessageEndpointDetails : IEndpointDetails
    {
        public MessageEndpointDetails(string address, EndpointType type)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Type = type;
        }

        public string Address { get; }

        public EndpointType Type { get; }

        public bool Equals(IEndpointDetails other)
        {
            if (other is not MessageEndpointDetails endpointDetails)
            {
                return false;
            }

            return string.Equals(Address, endpointDetails.Address, StringComparison.Ordinal) &&
                   Type == endpointDetails.Type;
        }

        public override bool Equals(object obj)
        {
            return obj is IEndpointDetails endpointDetails && Equals(endpointDetails);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Type);
        }
    }
}