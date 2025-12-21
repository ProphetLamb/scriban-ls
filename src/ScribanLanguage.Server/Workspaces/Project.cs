using ScribanLanguage.Services;

namespace ScribanLanguage.Workspaces;

[method: SetsRequiredMembers]
public sealed record Project(Uri RootUri, IServiceLifetime Lifetime) : LifetimeRecord(Lifetime)
{
    [field: AllowNull]
    public IListState<Document> Documents
    {
        get => field ??= ListState.Value<Document>(this, []);
        [UsedImplicitly] init;
    }
}