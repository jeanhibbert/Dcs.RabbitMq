using Dcs.RabbitMq.Messaging.Messaging;

namespace Dcs.RabbitMq.Messaging
{
    public sealed class RabbitMqEndpointDetails : IEndpointDetails
    {
        public string Address { get; private set; }
        public RabbitMqEndpointType Type { get; private set; }

        public RabbitMqEndpointDetails(string address, RabbitMqEndpointType type)
        {
            Address = address;
            Type = type;
        }

        public bool Equals(IEndpointDetails other)
        {
            if (string.CompareOrdinal(Address, other.Address) == 0 &&
                Type == ((RabbitMqEndpointDetails)other).Type)
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode() + this.Type.GetHashCode();
        }
    }
}
