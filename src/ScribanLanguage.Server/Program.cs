using System.CommandLine;
using ScribanLanguage.Commands;

RootCommand cli = new("Scriban Language Server")
{
    Options =
    {
        new VersionOption()
    },
    Subcommands =
    {
        new("stdio", "LSP JSON-RPC 2.0 over stdin and stdout stream")
        {
            Action = new StdioAction()
        },
        new("pipe", "LSP JSON-RPC 2.0 over named pipe stream")
        {
            Arguments =
            {
                NamedPipeAction.PipeNameArgument
            },
            Action = new NamedPipeAction()
        },
        new("tcp", "LSP JSON-RPC 2.0 over TCP steam")
        {
            Arguments =
            {
                TcpStreamAction.EndpointArgument
            },
            Action = new TcpStreamAction(),
        },
        new("udp", "LSP JSON-RPC 2.0 over UDP socket")
        {
            Arguments =
            {
                UdpSocketAction.EndpointArgument
            },
            Action = new UdpSocketAction(),
        },
    },
};

using CancellationTokenSource cts = new();
// ReSharper disable once AccessToDisposedClosure
Console.CancelKeyPress += (_, _) => cts.Cancel();

var parseResult = cli.Parse(args);
return await parseResult.InvokeAsync(cancellationToken: cts.Token).ConfigureAwait(false);