using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Threading;
using ScribanLanguage.Services;

namespace ScribanLanguage.Reactivity;

public interface IState<T> : IFeed<T>
{
    public ValueTask<Message<T>> UpdateMessage<TContext>(
        Func<Message<T>, TContext, CancellationToken, ValueTask<Message<T>>> updater,
        TContext context, CancellationToken cancellationToken = default);

    public ValueTask<TResult> ReadMessage<TResult, TContext>(
        Func<Message<T>, TContext, CancellationToken, ValueTask<TResult>> reader, TContext context,
        CancellationToken cancellationToken = default);

    protected void RegisterAttached(IDisposable? disposable);

    public static class Attached
    {
        public static void RegisterAttached(IState<T> state, IDisposable? disposable)
        {
            state.RegisterAttached(disposable);
        }
    }
}

public static class State
{
    private static StateImpl<T> Create<T>(ILifetimeMember owner, IStateSource<T> source)
    {
        var lt = owner.Lifetime;
        StateImpl<T> state = new(source, lt);
        lt.RegisterAttached(owner, state);
        return state;
    }

    public static IState<T> Empty<T>(ILifetimeMember owner) =>
        Create(owner,
            new StateSourceValue<T, object?>(null, null));

    public static IState<T> Value<T>(ILifetimeMember owner, T initialValue) =>
        Value(owner, static (initialValue, _) => ValueTask.FromResult(initialValue), initialValue);

    public static IState<T> Value<T>(ILifetimeMember owner, Func<T> initialValueFactory) =>
        Value(owner, static (initialValueFactory, _) => ValueTask.FromResult(initialValueFactory()),
            initialValueFactory);

    public static IState<T> Value<T>(ILifetimeMember owner,
        Func<CancellationToken, ValueTask<T>> initialValueFactory) =>
        Value(owner, static (initialValueFactory, ct) => initialValueFactory(ct), initialValueFactory);

    public static IState<T> Value<T, TContext>(ILifetimeMember owner,
        Func<TContext, CancellationToken, ValueTask<T>> initialValueFactory,
        TContext context) =>
        Create(owner,
            new StateSourceValue<T, TContext>(initialValueFactory, context));

    public static IState<T> Async<T>(ILifetimeMember owner,
        Func<CancellationToken, IAsyncEnumerable<T>> valuesStream) =>
        Async(owner, static (valuesStream, ct) => valuesStream(ct), valuesStream);

    public static IState<T> Async<T>(ILifetimeMember owner, IFeed<T> feed) =>
        Create(owner, new StateSourceFeed<T>(feed));

    public static IState<T> Async<T, TContext>(ILifetimeMember owner,
        Func<TContext, CancellationToken, IAsyncEnumerable<T>> valuesStream,
        TContext context) =>
        Create(owner,
            new StateSourceAsync<T, TContext>(valuesStream, context));

    [DebuggerDisplay("{_message,nq}")]
    private sealed class StateImpl<T> : IState<T>, IEquatable<IState<T>>, IObservable<Message<T>>, IDisposable
    {
        private readonly IServiceLifetime _lifetime;
        private readonly ConditionalWeakTable<IDisposable, IObserver<Message<T>>> _observers = new();
        private readonly List<IDisposable> _attached = [];
        private readonly AsyncReaderWriterLock _updateLock = new();
        private readonly Task _attachedSource;
        private Message<T> _message;

        [field: AllowNull]
        private ILogger RootLogger => field ??= _lifetime.Services.GetRequiredService<ILogger<StateImpl<T>>>();

        public StateImpl(IStateSource<T> source, IServiceLifetime lifetime)
        {
            _lifetime = lifetime;
            _attachedSource = AttachSource(source);
        }

        ~StateImpl()
        {
            Dispose(false);
        }

        private void ReleaseUnmanagedResources()
        {
            OnCompleted();
        }

        private void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                foreach (var disposable in _attached)
                {
                    disposable.Dispose();
                }

                _updateLock.Dispose();
                _attachedSource.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task AttachSource(IStateSource<T> source)
        {
            using var l = RootLogger.Function();
            try
            {
                await source.Attach(this, _lifetime.StoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                l.Error()?.Log(ex, "Source {Source} attached to state failed", source);
            }
        }

        public IDisposable Subscribe(IObserver<Message<T>> observer)
        {
            _ = GreetSubscriber(observer, _lifetime.StoppingToken);
            Handle handle = new(GCHandle.Alloc(this, GCHandleType.Weak));
            _observers.Add(handle, observer);
            return handle;
        }

        private async Task GreetSubscriber(IObserver<Message<T>> observer, CancellationToken cancellationToken)
        {
            using var l = RootLogger.Function();
            try
            {
                await using var r = (await _updateLock.ReadLockAsync(cancellationToken)).ConfigureAwait(false);
                if (_message.Current.HasData)
                {
                    observer.OnNext(_message.With().AdditionalMetadata([new GreetObserverMessageMetadata()]));
                }
            }
            catch (Exception ex)
            {
                l.Error()?.Log(ex, "Failed to greet subscriber {Observer}", observer);
            }
        }

        public async ValueTask<Message<T>> UpdateMessage<TContext>(
            Func<Message<T>, TContext, CancellationToken, ValueTask<Message<T>>> updater,
            TContext context, CancellationToken cancellationToken = default)
        {
            using var l = RootLogger.Function();

            Message<T> oldValue = default;
            Message<T> newValue = default!;
            Exception? error = null;
            try
            {
                await using var r = (await _updateLock.WriteLockAsync(cancellationToken)).ConfigureAwait(false);
                oldValue = _message;
                _message = newValue = await updater(_message, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                l.Error()?.Log(ex, "Error updating state value from {OldValue}", oldValue);
                error = ex;
            }

            if (error is not null)
            {
                OnError(error);
            }
            else if (newValue != oldValue)
            {
                OnNext(newValue);
            }

            return newValue;
        }

        public async ValueTask<TResult> ReadMessage<TResult, TContext>(
            Func<Message<T>, TContext, CancellationToken, ValueTask<TResult>> reader,
            TContext context,
            CancellationToken cancellationToken = default)
        {
            using var l = RootLogger.Function();
            Message<T> value = default;
            try
            {
                await using var r = (await _updateLock.ReadLockAsync(cancellationToken)).ConfigureAwait(false);
                value = _message;
                var result = await reader(value, context, cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                l.Error()?.Log(ex, "Error reading state of {Value}", value);
                throw;
            }
        }

        public void RegisterAttached(IDisposable? disposable)
        {
            if (disposable is not null)
                _attached.Add(disposable);
        }

        private void OnNext(Message<T> value)
        {
            foreach (var (_, o) in _observers)
            {
                o.OnNext(value);
            }
        }

        private void OnError(Exception error)
        {
            foreach (var (_, o) in _observers)
            {
                o.OnError(error);
            }
        }

        private void OnCompleted()
        {
            foreach (var (_, o) in _observers)
            {
                o.OnCompleted();
            }
        }

        private sealed class Handle(GCHandle state) : IDisposable
        {
            private GCHandle _state = state;

            public void Dispose()
            {
                if (_state is { IsAllocated: true, Target: StateImpl<T> s })
                {
                    s._observers.Remove(this);
                }

                _state.Free();
            }
        }

        private bool Equals(StateImpl<T> other)
        {
            return _message.Current == other._message.Current;
        }

        public bool Equals(IState<T>? other)
        {
            return other is StateImpl<T> impl && Equals(impl);
        }

        [MustDisposeResource]
        public IAsyncEnumerator<T> GetAsyncEnumerator(bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            return Changes().SelectMany(x => x.Current.TryGetData(out var data) ? [data] : Enumerable.Empty<T>())
                .ConfigureAwait(continueOnCapturedContext).WithCancellation(cancellationToken)
                .GetAsyncEnumerator();
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is StateImpl<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return _message.Current.GetHashCode();
        }

        public IObservable<Message<T>> Changes() => this;
    }

    private interface IStateSource<T>
    {
        ValueTask Attach(IState<T> state, CancellationToken cancellationToken);
    }

    private sealed class StateSourceValue<T, TContext>(
        Func<TContext, CancellationToken, ValueTask<T>>? initialValueFactory,
        TContext context)
        : IStateSource<T>
    {
        public async ValueTask Attach(IState<T> state, CancellationToken cancellationToken)
        {
            if (initialValueFactory is null)
            {
                return;
            }

            var initialValue = await initialValueFactory(context, cancellationToken).ConfigureAwait(false);
            await state.Update(initialValue, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class StateSourceAsync<T, TContext>(
        Func<TContext, CancellationToken, IAsyncEnumerable<T>> sequenceFactory,
        TContext context) : IStateSource<T>
    {
        public async ValueTask Attach(IState<T> state, CancellationToken cancellationToken)
        {
            var sequence = sequenceFactory(context, cancellationToken);
            await foreach (var item in sequence.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await state.Update(static (_, item, _) => new(item), item, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class StateSourceFeed<T>(IFeed<T> feed) : IStateSource<T>
    {
        public async ValueTask Attach(IState<T> state, CancellationToken cancellationToken)
        {
            await foreach (var item in feed.Changes())
            {
                await state.UpdateMessage(static (m, item, _) =>
                {
                    var b = m.With();
                    b = item.Current.TryGetData(out var data) ? b.Data(data) : b.Data();
                    b = b.Metadata(item.Current.Metadata);
                    return new(b.Build());
                }, item, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}