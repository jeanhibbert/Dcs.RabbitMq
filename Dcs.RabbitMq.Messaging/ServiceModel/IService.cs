using System;

namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    public interface IService : IDisposable
    {
        void Start();
    }
}
