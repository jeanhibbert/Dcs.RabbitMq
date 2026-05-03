using System;

namespace Dcs.Messaging.ServiceModel
{
    public interface IService : IDisposable
    {
        void Start();
    }
}
