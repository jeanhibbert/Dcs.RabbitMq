using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Dcs.Messaging.IntegrationTests.Infrastructure;

/// <summary>
/// Tiny helpers for awaiting Rx events with timeouts. Prefer these over raw
/// <c>FirstAsync().Wait()</c> calls so test failures surface as
/// <c>TimeoutException</c> rather than test runner deadlocks.
/// </summary>
internal static class AwaitableObservable
{
    public static async Task<T> FirstWithinAsync<T>(this IObservable<T> source, TimeSpan timeout)
    {
        return await source.Timeout(timeout).FirstAsync().ToTask().ConfigureAwait(false);
    }

    public static async Task<IList<T>> CollectWithinAsync<T>(
        this IObservable<T> source,
        int count,
        TimeSpan timeout)
    {
        return await source.Take(count).Timeout(timeout).ToList().ToTask().ConfigureAwait(false);
    }
}
