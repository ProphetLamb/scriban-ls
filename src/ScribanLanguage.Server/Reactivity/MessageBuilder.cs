namespace ScribanLanguage.Reactivity;

public readonly ref struct MessageBuilder<T>(Message<T> message)
{
    public MessageBuilder<T> Data()
    {
        MessageState<T> next = new(false, default!, message.Current.Metadata);
        return new();
    }

    public MessageBuilder<T> Data(T data)
    {
        return Build(new(true, data, message.Current.Metadata)).With();
    }

    public MessageBuilder<T> Metadata()
    {
        return Build(new(message.Current.HasData, message.Current.Data!, null)).With();
    }

    public MessageBuilder<T> AdditionalMetadata(IEnumerable<IMessageMetadata>? metadatas)
    {
        return Metadata((message.Current.Metadata ?? []).Concat(metadatas ?? []).ToList());
    }

    public MessageBuilder<T> Metadata(IReadOnlyList<IMessageMetadata>? metadatas)
    {
        return Build(new(message.Current.HasData, message.Current.Data!, metadatas)).With();
    }

    private Message<T> Build(MessageState<T> next)
    {
        return new(message.Current, next);
    }

    public Message<T> Build()
    {
        return message;
    }

    public static implicit operator Message<T>(MessageBuilder<T> b) => b.Build();
}