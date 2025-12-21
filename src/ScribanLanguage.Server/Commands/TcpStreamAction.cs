using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using StreamJsonRpc;

namespace ScribanLanguage.Commands;

public sealed class TcpStreamAction : LspAction
{
    public static Argument<string> EndpointArgument { get; } = new("endpoint")
    {
        Description = "Start the TCP server on this IP endpoint",
    };

    protected override void ConfigureServices(ParseResult parseResult, IServiceCollection services)
    {
        var endpoint = parseResult.GetRequiredValue(EndpointArgument);
        TcpListener tcp = new(IPEndPoint.Parse(endpoint));
        tcp.Start();
        base.ConfigureServices(parseResult, services);
        services.AddSingleton(tcp);
    }

    protected override JsonRpc CreateRpc(ParseResult parseResult, IServiceProvider serviceProvider)
    {
        var tcp = serviceProvider.GetRequiredService<TcpListener>();
        var client = tcp.AcceptTcpClient();
        return new(client.GetStream());
    }
}