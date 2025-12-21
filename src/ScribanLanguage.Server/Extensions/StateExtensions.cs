using System.Diagnostics;
using System.Reactive.Linq;

namespace ScribanLanguage.Extensions;

public static class StateExtensions
{
    public static async ValueTask<T> Update<T, TContext>(this IState<T> state,
        Func<T?, TContext, CancellationToken, ValueTask<T>> updater,
        TContext context, CancellationToken cancellationToken = default)
    {
        var result = await state.UpdateMessage(static async (e, t, ct) =>
            {
                var newData = await t.updater(e.Current.Data, t.context, ct).ConfigureAwait(false);
                return e.With().Data(newData).Metadata();
            }, (updater, context),
            cancellationToken).ConfigureAwait(false);
        return result.Current.Data!;
    }

    public static ValueTask<T> Update<T>(this IState<T> state, Func<T?, CancellationToken, ValueTask<T>> updater,
        CancellationToken cancellationToken = default)
    {
        return state.Update(static (e, updater, cancellationToken) => updater(e, cancellationToken), updater,
            cancellationToken);
    }

    public static ValueTask<T> Update<T, TContext>(this IState<T> state, Func<T?, TContext, T> updater,
        TContext context, CancellationToken cancellationToken = default)
    {
        return state.Update(static (e, t, _) => ValueTask.FromResult(t.updater(e, t.context)), (updater, context),
            cancellationToken);
    }

    public static ValueTask<T> Update<T>(this IState<T> state, Func<T?, T> updater,
        CancellationToken cancellationToken = default)
    {
        return state.Update(static (e, updater, _) => ValueTask.FromResult(updater(e)), updater,
            cancellationToken);
    }

    public static ValueTask<T> Update<T>(this IState<T> state, T newValue,
        CancellationToken cancellationToken = default)
    {
        return state.Update(static (e, newValue, _) => ValueTask.FromResult(newValue), newValue,
            cancellationToken);
    }

    public static ValueTask<T?> Read<T>(this IState<T> state, CancellationToken cancellationToken = default)
    {
        return state.Read(static m => m, cancellationToken);
    }

    public static ValueTask<TResult> Read<T, TResult>(this IState<T> state,
        Func<T?, TResult> reader, CancellationToken cancellationToken = default)
    {
        return state.Read(static (m, reader) => reader(m), reader, cancellationToken);
    }

    public static ValueTask<TResult> Read<T, TContext, TResult>(this IState<T> state,
        Func<T?, TContext, TResult> reader, TContext context, CancellationToken cancellationToken = default)
    {
        return state.Read(
            static (m, t, _) => ValueTask.FromResult(t.reader(m, t.context)), (reader, context), cancellationToken
        );
    }

    public static ValueTask<TResult> Read<T, TResult>(this IState<T> state,
        Func<T?, CancellationToken, ValueTask<TResult>> reader, CancellationToken cancellationToken = default)
    {
        return state.Read(
            static (m, reader, cancellationToken) => reader(m, cancellationToken), reader, cancellationToken
        );
    }

    public static ValueTask<TResult> Read<T, TResult, TContext>(this IState<T> state,
        Func<T?, TContext, CancellationToken, ValueTask<TResult>> reader, TContext context,
        CancellationToken cancellationToken = default)
    {
        return state.ReadMessage(
            static (m, t, cancellationToken) => t.reader(m.Current.Data, t.context, cancellationToken),
            (reader, context), cancellationToken
        );
    }

    public static IState<T> ForEach<T>(this IState<T> state,
        Action<T> action)
    {
        return ForEach(state, static (m, action) => action(m), action);
    }

    public static IState<T> ForEach<T, TContext>(this IState<T> state, Action<T, TContext> action, TContext context)
    {
        return ForEach(state, static (m, t, ct) =>
        {
            t.action(m, t.context);
            return default;
        }, (action, context));
    }

    public static IState<T> ForEach<T>(this IState<T> state,
        Func<T, CancellationToken, ValueTask> action)
    {
        return ForEach(state, static (m, action, ct) => action(m, ct), action);
    }

    public static IState<T> ForEach<T, TContext>(this IState<T> state,
        Func<T, TContext, CancellationToken, ValueTask> action, TContext context)
    {
        var changes = state.Changes().Where(x => x.Current.HasData);
        var disposable = ForEachMessage(changes, static (m, t, ct) => t.action(m.Current.Data!, t.context, ct),
            (action, context));
        IState<T>.Attached.RegisterAttached(state, disposable);
        return state;
    }

    public static IState<T> ForEachMessage<T, TContext>(this IState<T> state,
        Func<Message<T>, TContext, CancellationToken, ValueTask> action, TContext context)
    {
        var disposable = ForEachMessage(state.Changes(), action, context);
        IState<T>.Attached.RegisterAttached(state, disposable);
        return state;
    }

    private static IDisposable ForEachMessage<T, TContext>(IObservable<Message<T>> observable,
        Func<Message<T>, TContext, CancellationToken, ValueTask> action, TContext context)
    {
        return observable.Subscribe(new ForEachMessageSubscriber<T, TContext>(action, context));
    }

    private sealed class ForEachMessageSubscriber<T, TContext>(
        Func<Message<T>, TContext, CancellationToken, ValueTask> action,
        TContext context) : IObserver<Message<T>>
    {
        private readonly CancellationTokenSource _cts = new();

        public void OnCompleted()
        {
            _cts.Cancel();
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Message<T> value)
        {
            try
            {
                var maybeTask = action(value, context, _cts.Token);
                if (maybeTask.IsCompletedSuccessfully)
                {
                    return;
                }

                _ = OnNextAsync(maybeTask);
            }
            catch
            {
                // ignore
            }
        }

        private async Task OnNextAsync(ValueTask maybeTask)
        {
            try
            {
                await maybeTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }


    public static IListState<T> AsList<T>(this IState<IReadOnlyList<T>> state) =>
        state as IListState<T> ?? new ListStateFacade<T>(state);


    [DebuggerDisplay("{State,nq}")]
    private sealed class ListStateFacade<T>(IState<IReadOnlyList<T>> state)
        : IListState<T>, IObservable<Message<IReadOnlyList<T>>>
    {
        public IState<IReadOnlyList<T>> State => state;

        public IDisposable Subscribe(IObserver<Message<IReadOnlyList<T>>> observer)
        {
            return state.Changes().Subscribe(observer);
        }

        public ValueTask<Message<IReadOnlyList<T>>> UpdateMessage<TContext>(
            Func<Message<IReadOnlyList<T>>, TContext, CancellationToken, ValueTask<Message<IReadOnlyList<T>>>> updater,
            TContext context,
            CancellationToken cancellationToken = default)
        {
            return state.UpdateMessage(updater, context, cancellationToken);
        }

        public ValueTask<TResult> ReadMessage<TResult, TContext>(
            Func<Message<IReadOnlyList<T>>, TContext, CancellationToken, ValueTask<TResult>> reader,
            TContext context,
            CancellationToken cancellationToken = default)
        {
            return state.ReadMessage(reader, context, cancellationToken);
        }

        public void RegisterAttached(IDisposable? disposable)
        {
            IState<IReadOnlyList<T>>.Attached.RegisterAttached(state, disposable);
        }

        public IObservable<Message<IReadOnlyList<T>>> Changes() => this;

        public IAsyncEnumerator<IReadOnlyList<T>> GetAsyncEnumerator(bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            return state.GetAsyncEnumerator(continueOnCapturedContext, cancellationToken);
        }
    }
}