using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using StreamJsonRpc;

namespace ScribanLanguage.Commands;

public class UdpSocketAction : LspAction
{
    public static Argument<string> EndpointArgument { get; } = new("endpoint")
    {
        Description = "Start sending UDP on this IP endpoint",
    };

    protected override void ConfigureServices(ParseResult parseResult, IServiceCollection services)
    {
        var endpoint = parseResult.GetRequiredValue(EndpointArgument);
        UdpClient listener = new(IPEndPoint.Parse(endpoint));
        base.ConfigureServices(parseResult, services);
        services.AddSingleton(listener);
    }

    protected override JsonRpc CreateRpc(ParseResult parseResult, IServiceProvider serviceProvider)
    {
        var udp = serviceProvider.GetRequiredService<UdpClient>();
        return new(new NetworkStream(udp.Client, FileAccess.ReadWrite));
    }
}