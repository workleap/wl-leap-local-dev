namespace Leap.Cli.Aspire;

internal interface IAspireManager
{
    IDistributedApplicationBuilder Builder { get; }

    void BeginAspireWorkloadDownloadTask(CancellationToken cancellationToken);

    Task<DistributedApplication> StartAppHostAsync(CancellationToken cancellationToken);
}