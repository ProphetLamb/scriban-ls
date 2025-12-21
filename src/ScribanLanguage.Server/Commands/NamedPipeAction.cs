using System.CommandLine;
using System.IO.Pipes;
using StreamJsonRpc;

namespace ScribanLanguage.Commands;

public sealed class NamedPipeAction : LspAction
{
    public static Argument<string> PipeNameArgument { get; } = new("name")
    {
        Description = "Start the named pipe server on this pipe name",
    };

    protected override void ConfigureServices(ParseResult parseResult, IServiceCollection services)
    {
        base.ConfigureServices(parseResult, services);
        var pipeName = parseResult.GetRequiredValue(PipeNameArgument);
        NamedPipeServerStream server = new(pipeName, PipeDirection.InOut);
        services.AddSingleton(server);
    }

    protected override JsonRpc CreateRpc(ParseResult parseResult, IServiceProvider serviceProvider)
    {
        var server = serviceProvider.GetRequiredService<NamedPipeServerStream>();
        server.WaitForConnection();
        return new(server);
    }
}
