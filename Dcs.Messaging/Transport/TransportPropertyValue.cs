using Dcs.RabbitMq.Messaging.Messaging;
using ProtoBuf;
using System;

namespace Dcs.RabbitMq.Messaging.Transport
{
    [ProtoContract]
    public sealed class TransportPropertyValue
    {
        [ProtoMember(1)]
        public string BoolValue { get; set; }

        [ProtoMember(2)]
        public string DoubleValue { get; set; }

        [ProtoMember(3)]
        public string IntValue { get; set; }

        [ProtoMember(4)]
        public string Key { get; set; }

        [ProtoMember(5)]
        public string Kind { get; set; }

        [ProtoMember(6)]
        public string StringValue { get; set; }

        public void ApplyTo(MessageProperties properties)
        {
            switch (Kind)
            {
                case "bool":
                    properties.Set(Key, bool.Parse(BoolValue));
                    break;
                case "double":
                    properties.Set(Key, double.Parse(DoubleValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "int":
                    properties.Set(Key, int.Parse(IntValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    properties.Set(Key, StringValue);
                    break;
            }
        }

        public static TransportPropertyValue From(string key, object value)
        {
            switch (value)
            {
                case bool boolValue:
                    return new TransportPropertyValue { BoolValue = boolValue.ToString(), Key = key, Kind = "bool" };
                case double doubleValue:
                    return new TransportPropertyValue { DoubleValue = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Key = key, Kind = "double" };
                case int intValue:
                    return new TransportPropertyValue { IntValue = intValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Key = key, Kind = "int" };
                default:
                    return new TransportPropertyValue { Key = key, Kind = "string", StringValue = Convert.ToString(value) };
            }
        }
    }
}
