using Microsoft.VisualStudio.LanguageServer.Protocol;
using ScribanLanguage.Services;

namespace ScribanLanguage.Workspaces;

[method: SetsRequiredMembers]
public sealed record Workspace(IServiceLifetime Lifetime) : LifetimeRecord(Lifetime)
{
    [field: AllowNull]
    public IListState<Project> Projects
    {
        get => field ??= ListState.Value<Project>(this, []);
        [UsedImplicitly] init;
    }

    [field: AllowNull]
    public IState<HostConfiguration> Host
    {
        get => field ??= State.Empty<HostConfiguration>(this);
        [UsedImplicitly] init;
    }
}

public sealed record HostConfiguration(
    int? ProcessId,
    LogLevel LogLevel,
    ClientCapabilities ClientCapabilities,
    ServerCapabilities ServerCapabilities);