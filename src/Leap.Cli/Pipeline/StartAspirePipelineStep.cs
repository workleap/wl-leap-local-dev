using Leap.Cli.Aspire;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class StartAspirePipelineStep(IAspireManager aspireManager) : IPipelineStep
{
    private DistributedApplication? _app;

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        // No need to start aspire if there are no services to run
        if (state.Services.Count == 0)
        {
            return;
        }

        this._app = await aspireManager.StartAppHostAsync(cancellationToken);
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._app != null)
        {
            await this._app.StopAsync(cancellationToken);
            await this._app.DisposeAsync();
        }
    }
}
