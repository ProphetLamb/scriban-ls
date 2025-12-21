using ScribanLanguage.Services;

namespace ScribanLanguage.Reactivity;

public interface IListState<T> : IState<IReadOnlyList<T>>;

public static class ListState
{
    public static IListState<T> Empty<T>(ILifetimeMember owner) => State.Empty<IReadOnlyList<T>>(owner).AsList();

    public static IListState<T> Value<T>(ILifetimeMember owner, IReadOnlyList<T> initialValue) =>
        State.Value(owner, initialValue).AsList();

    public static IListState<T> Value<T>(ILifetimeMember owner, Func<IReadOnlyList<T>> initialValueFactory) =>
        State.Value(owner, initialValueFactory).AsList();

    public static IListState<T> Value<T, TContext>(ILifetimeMember owner,
        Func<TContext, CancellationToken, ValueTask<IReadOnlyList<T>>> initialValueFactory,
        TContext context) => State.Value(owner, initialValueFactory, context).AsList();

    public static IListState<T> Async<T>(ILifetimeMember owner,
        Func<CancellationToken, IAsyncEnumerable<IReadOnlyList<T>>> valuesStream) =>
        State.Async(owner, static (valuesStream, ct) => valuesStream(ct), valuesStream).AsList();

    public static IListState<T> Async<T, TContext>(ILifetimeMember owner,
        Func<TContext, CancellationToken, IAsyncEnumerable<IReadOnlyList<T>>> valuesStream,
        TContext context) => State.Async(owner, valuesStream, context).AsList();
}