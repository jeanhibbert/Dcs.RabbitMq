using System.Collections.Generic;

namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IMessageProperties
    {
        string GetString(string key);
        bool GetBoolean(string key);
        int GetInt32(string key);
        double GetDouble(string key);
        void Set(string key, string value);
        void Set(string key, bool value);
        void Set(string key, int value);
        void Set(string key, double value);
        IEnumerable<string> Keys { get; }
        IEnumerable<object> Values { get; }
    }
}