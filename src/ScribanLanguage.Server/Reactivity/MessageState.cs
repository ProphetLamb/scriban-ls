using System.Diagnostics;

namespace ScribanLanguage.Reactivity;

[DebuggerDisplay("{Data,nq}")]
public readonly struct MessageState<T>(bool hasData, T data, IReadOnlyList<IMessageMetadata>? metadata)
    : IEquatable<MessageState<T>>
{
    public IReadOnlyList<IMessageMetadata>? Metadata => metadata;
    public bool HasData => hasData;
    public T? Data => hasData ? data : default;

    public bool TryGetData([MaybeNullWhen(false)] out T result)
    {
        result = data;
        return hasData;
    }

    public bool Equals(MessageState<T> other)
    {
        return (HasData, other.HasData) switch
        {
            (true, true) => EqualityComparer<T>.Default.Equals(Data, other.Data),
            (false, false) => true,
            _ => false,
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is MessageState<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HasData ? HashCode.Combine(Data) : 0;
    }

    public static bool operator ==(MessageState<T> lhs, MessageState<T> rhs) => lhs.Equals(rhs);

    public static bool operator !=(MessageState<T> lhs, MessageState<T> rhs) => !(lhs == rhs);
}