using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

// Detects CS2012 errors when building .NET projects. This might be a race condition where two projects
// referencing the same class library are built at the same time. The workaround is to restart Leap.
//
// This class is HEAVILY inspired from the lifecycle hook in .NET Aspire that starts the dashboard
// as an executable resource and streams its logs to the console output. See:
// https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dashboard/DashboardLifecycleHook.cs
internal sealed class DetectDotnetBuildRaceConditionErrorLifecycleHook(
    ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService,
    ILogger<DetectDotnetBuildRaceConditionErrorLifecycleHook> lifecycleHookLogger) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task? _dotnetLogsTasks;

    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        this._dotnetLogsTasks = this.WatchDotnetLogsAsync(this._shutdownCts.Token);
        return Task.CompletedTask;
    }

    private async Task WatchDotnetLogsAsync(CancellationToken cancellationToken)
    {
        var dotnetResourceTasks = new Dictionary<string, Task>(StringComparer.Ordinal);

        try
        {
            await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!dotnetResourceTasks.ContainsKey(notification.ResourceId) && notification.Resource is ExecutableResource { Command: "dotnet" })
                {
                    dotnetResourceTasks[notification.ResourceId] = this.WatchDotnetResourceLogsAsync(notification.ResourceId, notification.Resource.Name, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the application is shutting down.
        }
        catch (Exception ex)
        {
            lifecycleHookLogger.LogError(ex, "An error occurred while reading dotnet resource logs in order to detect CS2012 race condition compilation errors.");
        }

        // The watch task should already be logging exceptions, so we don't need to log them here.
        await Task.WhenAll(dotnetResourceTasks.Values).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task WatchDotnetResourceLogsAsync(string resourceId, string resourceName, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var batch in resourceLoggerService.WatchAsync(resourceId).WithCancellation(cancellationToken))
            {
                foreach (var logLine in batch)
                {
                    if (logLine.Content.Contains("error CS2012:", StringComparison.OrdinalIgnoreCase))
                    {
                        // CS2012 docs: https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs2012
                        // .NET Aspire restart GitHub issue: https://github.com/dotnet/aspire/issues/295
                        lifecycleHookLogger.LogError(
                            "Service '{ServiceName}' failed to build, likely due to a CS2012 race condition caused by another service currently being built that uses a same shared class library. Please restart Leap until services can be restarted from the dashboard.",
                            resourceName);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the application is shutting down.
        }
        catch (Exception ex)
        {
            lifecycleHookLogger.LogError(ex, "An error occurred while reading service '{ServiceName}' logs in order to detect CS2012 race condition compilation errors.", resourceName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this._shutdownCts.CancelAsync();

        if (this._dotnetLogsTasks != null)
        {
            try
            {
                await this._dotnetLogsTasks.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the application is shutting down.
            }
            catch (Exception ex)
            {
                lifecycleHookLogger.LogError(ex, "An error occurred while reading dotnet resource logs in order to detect CS2012 race condition compilation errors.");
            }
        }

        this._shutdownCts.Dispose();
    }
}