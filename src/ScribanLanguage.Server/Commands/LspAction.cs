using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Hosting;
using ScribanLanguage.Controllers;
using ScribanLanguage.Logging;
using ScribanLanguage.Servers;
using ScribanLanguage.Services;
using ScribanLanguage.Workspaces;
using StreamJsonRpc;

namespace ScribanLanguage.Commands;

public abstract class LspAction : AsynchronousCommandLineAction
{
    protected abstract JsonRpc CreateRpc(ParseResult parseResult, IServiceProvider serviceProvider);


    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        HostBuilder b = new();
        b.ConfigureServices(s => ConfigureServices(parseResult, s));
        using var host = b.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
        return Environment.ExitCode;
    }

    protected virtual void ConfigureServices(ParseResult parseResult, IServiceCollection services)
    {
        services
            .AddSingleton<ILoggerProvider, JsonRpcLoggerProvider>()
            .AddLogging();
        services
            .AddServiceLifetime()
            .AddSingleton<DiagnosticServer>()
            .AddSingleton<SemanticTokenServer>()
            .AddSingleton<DocumentSyncServer>()
            .AddSingleton<CapabilitiesServer>()
            .AddSingleton<Workspace>()
            .AddTransient<WorkspaceNavigator>()
            .AddSingleton(sp => CreateRpc(parseResult, sp))
            .AddTransient<IJsonRpcController, LanguageServerJsonRpcController>()
            .AddHostedService<JsonRpcService>()
            .AddHostedService<WatchDogService>()
            ;
    }
}