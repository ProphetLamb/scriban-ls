namespace ScribanLanguage.Reactivity;

public interface IFeed<T> : ISignal<Message<T>>
{
    [MustDisposeResource]
    public IAsyncEnumerator<T> GetAsyncEnumerator(bool continueOnCapturedContext, CancellationToken cancellationToken);
}