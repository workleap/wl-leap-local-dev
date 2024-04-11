using Aspire.Hosting;

namespace Leap.Cli.Aspire;

internal interface IAspireManager
{
    IDistributedApplicationBuilder Builder { get; }

    Task<IAsyncDisposable> StartAsync(CancellationToken cancellationToken);
}
