using System.Diagnostics;

namespace ScribanLanguage.Reactivity;

[DebuggerDisplay("{Current,nq}")]
public readonly struct Message<T>(MessageState<T> previous, MessageState<T> current) : IEquatable<Message<T>>
{
    public MessageState<T> Previous => previous;
    public MessageState<T> Current => current;

    public bool Equals(Message<T> other)
    {
        return Current == other.Current;
    }

    public override bool Equals(object? obj)
    {
        return obj is Message<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Current.GetHashCode();
    }

    public MessageBuilder<T> With() => new(this);

    public static bool operator ==(Message<T> lhs, Message<T> rhs) => lhs.Equals(rhs);

    public static bool operator !=(Message<T> lhs, Message<T> rhs) => !(lhs == rhs);
}