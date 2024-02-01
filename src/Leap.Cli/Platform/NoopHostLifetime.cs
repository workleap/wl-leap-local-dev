using Microsoft.Extensions.Hosting;

namespace Leap.Cli.Platform;

internal sealed class NoopHostLifetime : IHostLifetime
{
    // TODO get inspiration from console lifetime, hook to IHostApplicationLifetime and print messages when the app is started and stopped
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}