using System.Reactive.Linq;

namespace ScribanLanguage.Extensions;

public static class FeedExtensions
{
    public static IFeed<T> AsFeed<T>(this IObservable<Message<T>> observable)
    {
        return observable as IFeed<T> ?? new ObservableFeed<T>(observable);
    }

    private sealed class ObservableFeed<T>(IObservable<Message<T>> observable) : IFeed<T>, IObservable<Message<T>>
    {
        public IObservable<Message<T>> Changes() => this;

        public IDisposable Subscribe(IObserver<Message<T>> observer)
        {
            return observable.Subscribe(observer);
        }

        [MustDisposeResource]
        public IAsyncEnumerator<T> GetAsyncEnumerator(bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            return Changes().SelectMany(x => x.Current.TryGetData(out var data) ? [data] : Enumerable.Empty<T>())
                .ConfigureAwait(continueOnCapturedContext).WithCancellation(cancellationToken)
                .GetAsyncEnumerator();
        }
    }

    public readonly struct ConfiguredAsyncEnumerableFeed<T>(
        IFeed<T> feed,
        bool continueOnCapturedContext,
        CancellationToken ct)
    {
        public IFeed<T> Feed => feed;
        public bool ContinueOnCapturedContext => continueOnCapturedContext;
        public CancellationToken CancellationToken => ct;

        public IAsyncEnumerator<T> GetAsyncEnumerator()
        {
            return Feed.GetAsyncEnumerator(ContinueOnCapturedContext, CancellationToken);
        }

        public ConfiguredAsyncEnumerableFeed<T> WithCancellation(CancellationToken cancellationToken) =>
            new(feed, continueOnCapturedContext, cancellationToken);
    }

    [MustDisposeResource]
    public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this IFeed<T> feed)
    {
        return feed.GetAsyncEnumerator(true, CancellationToken.None);
    }

    public static ConfiguredAsyncEnumerableFeed<T>
        ConfigureAwait<T>(this IFeed<T> feed, bool continueOnCapturedContext) =>
        new(feed, continueOnCapturedContext, CancellationToken.None);

    public static IFeed<TOut> SelectMany<TIn, TOut>(this IFeed<TIn> feed, Func<TIn, IEnumerable<TOut>> selector)
    {
        return feed.Changes().SelectMany(m =>
        {
            var b = default(Message<TOut>).With().Metadata(m.Current.Metadata).Build();
            return m.Current.TryGetData(out var data) ? selector(data).Select(d => b.With().Data(d).Build()) : [];
        }).AsFeed();
    }

    public static async ValueTask<T> Value<T>(this IFeed<T> feed, CancellationToken cancellationToken = default)
    {
        return await feed.Changes()
            .SelectMany(x => x.Current.TryGetData(out var data) ? [data] : Enumerable.Empty<T>())
            .FirstAsync()
            .RunAsync(cancellationToken);
    }

    public static IFeed<TOut> SelectMany<TIn, TOut>(this IFeed<TIn> feed,
        Func<TIn, IFeed<TOut>> selector)
    {
        return feed.Changes().SelectMany(m => m.Current.TryGetData(out var data)
            ? selector(data).Changes()
            : Observable.Empty<Message<TOut>>()).AsFeed();
    }

    public static IFeed<TOut> SelectMany<TIn, TOut>(this IFeed<TIn> feed,
        Func<TIn, IObservable<TOut>> selector)
    {
        return feed.Changes().SelectMany(m =>
        {
            var b = default(Message<TOut>).With().Metadata(m.Current.Metadata).Build();
            return m.Current.TryGetData(out var data)
                ? selector(data).Select(d => b.With().Data(d).Build())
                : Observable.Empty<Message<TOut>>();
        }).AsFeed();
    }

    public static IFeed<TOut> Select<TIn, TOut>(this IFeed<TIn> feed, Func<TIn, TOut> selector)
    {
        return feed.Changes().SelectMany(m =>
            {
                var b = default(Message<TOut>).With().Metadata(m.Current.Metadata);
                return m.Current.TryGetData(out var data)
                    ? [b.Data(selector(data))]
                    : Enumerable.Empty<Message<TOut>>();
            })
            .AsFeed();
    }
}