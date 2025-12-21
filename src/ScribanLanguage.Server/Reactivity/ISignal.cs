namespace ScribanLanguage.Reactivity;

public interface ISignal<out T>
{
    public IObservable<T> Changes();
}