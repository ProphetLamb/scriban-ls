namespace ScribanLanguage.Reactivity;

public interface IMessageMetadata
{
    public string Name { get; }
}

public sealed class GreetObserverMessageMetadata : IMessageMetadata
{
    public string Name => "greet-observer";
}