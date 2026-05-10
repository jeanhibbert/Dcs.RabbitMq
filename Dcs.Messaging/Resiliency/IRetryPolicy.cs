using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Executes a connection / network operation with retry semantics.
    /// Implementations MUST NOT propagate transport exceptions out of
    /// <see cref="ExecuteAsync(Func{CancellationToken, Task{bool}}, CancellationToken)"/>
    /// unless cancellation was requested. Returning false from the operation
    /// (or throwing a non-cancel exception) is treated as a failed attempt and
    /// the policy will back off and retry.
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// Repeatedly invokes <paramref name="operation"/> until it returns
        /// <c>true</c> (success), <paramref name="cancellationToken"/> is
        /// cancelled, or the policy gives up.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the operation eventually succeeded, <c>false</c>
        /// when the policy gave up for non-cancellation reasons.
        /// </returns>
        Task<bool> ExecuteAsync(
            Func<CancellationToken, Task<bool>> operation,
            CancellationToken cancellationToken);
    }
}
