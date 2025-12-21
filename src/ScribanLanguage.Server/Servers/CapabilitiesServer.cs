using Microsoft.VisualStudio.LanguageServer.Protocol;
using ScribanLanguage.Logging;
using ScribanLanguage.Lsp;
using ScribanLanguage.Workspaces;
using StreamJsonRpc;

namespace ScribanLanguage.Servers;

public sealed class CapabilitiesServer(Workspace workspace, JsonRpc rpc)
{
    public async ValueTask EnsureAllowed(string controllerMethodName, CancellationToken cancellationToken)
    {
        var config = await workspace.Host.Read(cancellationToken).ConfigureAwait(false);
        if (config is null && !StringComparer.Ordinal.Equals(controllerMethodName, nameof(Methods.Initialize)))
        {
            throw new RemoteInvocationException("Uninitialized", -32002, default(object?));
        }
    }


    public async ValueTask<InitializeResult> Initialize(InitializeParams p,
        CancellationToken cancellationToken)
    {
        var capabilities = GetServerCapabilities();
        await UpdateHostConfiguration(p, capabilities, cancellationToken).ConfigureAwait(false);

        if (p.RootUri is { } root)
        {
            await TryCreateProjectForUri(root, cancellationToken).ConfigureAwait(false);
        }

        return new()
        {
            Capabilities = capabilities,
        };
    }

    private static ServerCapabilities GetServerCapabilities()
    {
        return new()
        {
            TextDocumentSync = new()
            {
                OpenClose = true,
                Change = TextDocumentSyncKind.Incremental,
                Save = new SaveOptions
                {
                    IncludeText = true,
                },
            },
            CompletionProvider = new()
            {
                ResolveProvider = true,
                TriggerCharacters = [",", ".", "("]
            },
            HoverProvider = true,
            DefinitionProvider = true,
            ReferencesProvider = true,
            DocumentFormattingProvider = true,
            RenameProvider = true,
            SemanticTokensOptions = new()
            {
                Full = true,
                Range = true,
                Legend = new()
                {
                    TokenTypes =
                    [
                        SemanticTokenTypes.Variable,
                        SemanticTokenTypes.Comment,
                        SemanticTokenTypes.String,
                        SemanticTokenTypes.Keyword,
                        SemanticTokenTypes.Operator,
                        SemanticTokenTypes.Function,
                        SemanticTokenTypes.Parameter,
                        SemanticTokenTypes.Property,
                    ],
                    TokenModifiers =
                    [
                        SemanticTokenModifiers.Static,
                        SemanticTokenModifiers.Declaration,
                        SemanticTokenModifiers.Documentation,
                    ]
                },
            },
        };
    }

    private async ValueTask UpdateHostConfiguration(InitializeParams init, ServerCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        await workspace.Host
            .Update(
                new HostConfiguration(init.ProcessId, JsonRpcLoggerProvider.TraceSettingToLogLevel(init.Trace),
                    init.Capabilities, capabilities),
                cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask TryCreateProjectForUri(Uri uri, CancellationToken cancellationToken)
    {
        var project = new Project(uri, workspace.Lifetime);
        await workspace.Projects.Update(static (d, project) =>
                (d ?? []).Any(x => x.RootUri.IsBaseOf(project.RootUri))
                    ? (d ?? [])
                    : (d ?? []).Append(project).ToList(),
            project, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetTrace(SetTraceParams p, CancellationToken cancellationToken)
    {
        await workspace.Host.Update(
            static (h, p) => h! with { LogLevel = JsonRpcLoggerProvider.TraceSettingToLogLevel(p.Value) }, p,
            cancellationToken).ConfigureAwait(false);
    }

    public ValueTask Initialized(InitializedParams p, CancellationToken cancellationToken)
    {
        return default;
    }
}