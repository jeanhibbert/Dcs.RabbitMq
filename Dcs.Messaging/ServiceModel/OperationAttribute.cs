using System;

namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class OperationAttribute : Attribute
    {
        public bool Transactional { get; private set; }

        protected OperationAttribute(bool transactional)
        {
            Transactional = transactional;
        }

        protected OperationAttribute()
            : this(false)
        { }
    }
}
