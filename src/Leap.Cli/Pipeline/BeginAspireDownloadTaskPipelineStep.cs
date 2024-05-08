using Leap.Cli.Aspire;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class BeginAspireDownloadTaskPipelineStep(IAspireManager aspireManager) : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        // We download the Aspire workload packages ourselves so users won't have to install the workload themselves
        // using "dotnet workload install aspire", which cannot be used with a specific version and requires admin rights
        aspireManager.BeginAspireWorkloadDownloadTask(cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}