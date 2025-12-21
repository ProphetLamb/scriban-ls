using Microsoft.Extensions.Hosting;
using StreamJsonRpc;

namespace ScribanLanguage.Services;

public interface IJsonRpcController
{
    JsonRpcTargetOptions? RpcOptions => null;
}

public sealed class JsonRpcService(
    JsonRpc rpc,
    IEnumerable<IJsonRpcController> controllers,
    IActivityTracingStrategy? tracingStrategy = null) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var controller in controllers)
        {
            rpc.AddLocalRpcTarget((object)controller, controller.RpcOptions);
        }

        rpc.ActivityTracingStrategy = tracingStrategy ?? rpc.ActivityTracingStrategy;
        rpc.StartListening();

        return rpc.Completion;
    }
}