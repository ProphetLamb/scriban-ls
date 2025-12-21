using Microsoft.VisualStudio.LanguageServer.Protocol;
using ScribanLanguage.Servers;
using ScribanLanguage.Services;

namespace ScribanLanguage.Workspaces;

[method: SetsRequiredMembers]
public sealed record Document(Uri Uri, int Version, string? LanguageId, IServiceLifetime Lifetime)
    : LifetimeRecord(Lifetime)
{
    [field: AllowNull]
    public IState<TextContent> Content
    {
        get => field ??= State.Value(this, TextContent.Empty);
        [UsedImplicitly] init;
    }

    [field: AllowNull]
    public IState<Scriban.Template> Template
    {
        get => field ??= State.Async(this,
                Content.Select(x =>
                    Lifetime.Services.GetRequiredService<SemanticTokenServer>().ParseTextContent(x, this)))
            .ForEach(static (x, self, cancellationToken) => self.Lifetime.Services
                .GetRequiredService<DiagnosticServer>()
                .PublishParserDiagnostics(x, self, cancellationToken), this);
        [UsedImplicitly] init;
    }
}