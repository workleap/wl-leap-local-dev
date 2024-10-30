using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal sealed class AspireDashboardReadinessAwaiter(ResourceNotificationService resourceNotificationService) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cts = new();

    private Task? _monitorTask;
    private int _disposed;

    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        this._monitorTask = this.MonitorDashboardReadinessAsync(this._cts.Token);
        return Task.CompletedTask;
    }

    private async Task MonitorDashboardReadinessAsync(CancellationToken cancellationToken)
    {
        // Can't use "IDistributedApplicationEventing.Subscribe<ResourceReadyEvent>(...)" because hidden resources like the dashboard don't emit events
        await foreach (var resourceEvent in resourceNotificationService.WatchAsync(cancellationToken))
        {
            // Can't check the dashboard state which is always "Hidden" (instead of "Starting" or "Running"),
            // nor its healthcheck which is always "Healthy" by default. StartTimeStamp seems to be only set when actually started.
            if (resourceEvent.Resource.Name == "aspire-dashboard" && resourceEvent.Snapshot.StartTimeStamp.HasValue)
            {
                this._tcs.SetResult(true);
                return;
            }
        }
    }

    public Task WaitForDashboardReadyAsync(CancellationToken cancellationToken)
    {
        return this._tcs.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // Class is registered twice (as its own type and as a lifecycle hook), making sure DisposeAsync is idempotent
        if (Interlocked.Exchange(ref this._disposed, 1) == 1)
        {
            return;
        }

        await this._cts.CancelAsync();

        if (this._monitorTask != null)
        {
            try
            {
                await this._monitorTask;
            }
            catch
            {
                // ignored
            }
        }

        this._cts.Dispose();
    }
}