using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Dcs.RabbitMq.Messaging.Extensions
{
    public static class ObservableExtensions
    {
        public static IObservable<T> OnSubscribe<T>(this IObservable<T> source, Action action)
        {
            return Observable.Create<T>(observer =>
            {
                var disposable = source.Subscribe(observer);
                action();
                return disposable;
            });
        }

        /// <summary>
        /// Returns a hot observable, actively subscribed to the underlying
        /// observable, remembering its single result.
        /// </summary>
        public static IObservable<T> PruneHot<T>(this IObservable<T> observable)
        {
            //var res = observable.Prune();
            //res.Connect();
            return observable;
        }

        public static IObservable<T> ReturnAsync<T>(
            Func<T> factory,
            IScheduler scheduler = null)
        {
            scheduler = scheduler ?? Scheduler.Default;

            AsyncSubject<T> res = new AsyncSubject<T>();
            scheduler.Schedule(() =>
            {
                try
                {
                    res.OnNext(factory());
                    res.OnCompleted();
                }
                catch (Exception ex)
                {
                    res.OnError(ex);
                }
            });
            return res;
        }

        public static T As<T>(this T obj)
        {
            return obj;
        }
    }
}
