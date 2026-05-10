using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Trivial <see cref="IRetryPolicy"/> that runs the operation exactly once.
    /// Useful in tests.
    /// </summary>
    public sealed class NoRetryPolicy : IRetryPolicy
    {
        public static readonly NoRetryPolicy Instance = new NoRetryPolicy();

        public async Task<bool> ExecuteAsync(
            Func<CancellationToken, Task<bool>> operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                return false;
            }

            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
