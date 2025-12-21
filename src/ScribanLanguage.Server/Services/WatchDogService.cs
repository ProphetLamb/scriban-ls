using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using ScribanLanguage.Workspaces;

namespace ScribanLanguage.Services;

public sealed class WatchDogService(
    Workspace workspace,
    ILogger<WatchDogService> rootLogger,
    IServiceLifetime serviceLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var l = rootLogger.Function();

        await foreach (var config in workspace.Host)
        {
            if (config.ProcessId is { } pid)
            {
                await ProcessExit(pid, stoppingToken).ConfigureAwait(false);
                serviceLifetime.Exit();
            }
            else
            {
                l.Error()?.Log("Unable to watch for host process exit, due to missing PID in initialization {Init}",
                    JsonConvert.SerializeObject(config));
            }
        }
    }

    private Task ProcessExit(int pid, CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();
        TaskCompletionSource tcs = new();
        cancellationToken.Register(static o => ((TaskCompletionSource)o!).TrySetCanceled(), tcs);
        _ = PollExit();
        try
        {
            var host = Process.GetProcessById(pid);
            host.Exited += (_, _) => tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            l.Error()?.Log(ex, "Failed to watch for host exit event");
        }

        return tcs.Task;

        async Task PollExit()
        {
            using var l = rootLogger.Function();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                try
                {
                    _ = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    // process isnt running
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    l.Error()?.Log(ex, "Failed to poll for host exit");
                }
            }
        }
    }
}