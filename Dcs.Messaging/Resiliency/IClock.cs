using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Abstraction over the system clock so resiliency code can be unit-tested
    /// without real wall-clock waits.
    /// </summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }

        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }
}
