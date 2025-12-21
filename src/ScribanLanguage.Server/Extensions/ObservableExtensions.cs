using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace ScribanLanguage.Extensions;

public static class ObservableExtensions
{
    public static ConfiguredObservable<T> ConfigureAwait<T>(this IObservable<T> observable,
        bool continueOnCapturedContext)
    {
        return new(observable, continueOnCapturedContext, default);
    }

    public static ConfiguredObservable<T> ConfigureAwait<T>(this ConfiguredObservable<T> observable,
        bool continueOnCapturedContext)
    {
        return new(observable.Observable, continueOnCapturedContext, observable.CancellationToken);
    }

    public static ConfiguredObservable<T> WithCancellation<T>(this ConfiguredObservable<T> observable,
        CancellationToken cancellationToken)
    {
        return new(observable.Observable, observable.ContinueOnCapturedContext, cancellationToken);
    }

    public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this IObservable<T> observable)
    {
        return observable.ConfigureAwait(true).GetAsyncEnumerator();
    }

    public readonly struct ConfiguredObservable<T>(
        IObservable<T> observable,
        bool continueOnCapturedContext,
        CancellationToken cancellationToken)
    {
        public IObservable<T> Observable => observable;
        public bool ContinueOnCapturedContext => continueOnCapturedContext;
        public CancellationToken CancellationToken => cancellationToken;

        public IAsyncEnumerator<T> GetAsyncEnumerator()
        {
            ObservableAsyncEnumerator<T> en = new(this);
            en.Subscription = Observable.Subscribe(en);
            return en;
        }
    }

    private sealed class ObservableAsyncEnumerator<T>(ConfiguredObservable<T> observable)
        : IAsyncEnumerator<T>, IObserver<T>
    {
        private readonly Channel<Message> _items = Channel.CreateUnbounded<Message>();
        private T? _current;

        public IDisposable? Subscription { get; set; }

        public ValueTask DisposeAsync()
        {
            if (_items.Writer.TryComplete())
            {
                Subscription?.Dispose();
            }

            return default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            var next = _items.Reader.ReadAsync(observable.CancellationToken);
            if (next.IsCanceled)
            {
                return false;
            }

            var message = await next.ConfigureAwait(false);
            if (message.Item is { } item)
            {
                _current = item;
                return true;
            }

            if (message.Error is { } ex)
            {
                ex.Throw();
                return true;
            }

            return false;
        }

        public T Current => _current ?? throw new NullReferenceException("Current is null");

        public void OnCompleted()
        {
            if (_items.Writer.TryComplete())
            {
                Subscription?.Dispose();
            }
        }

        public void OnError(Exception error)
        {
            _items.Writer.TryWrite(new(default, ExceptionDispatchInfo.Capture(error)));
        }

        public void OnNext(T value)
        {
            _items.Writer.TryWrite(new(value, null));
        }

        private readonly struct Message(T? item, ExceptionDispatchInfo? error)
        {
            public T? Item => item;
            public ExceptionDispatchInfo? Error => error;
        }
    }
}