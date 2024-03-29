using Leap.Cli.Aspire;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class StartAspirePipelineStep : IPipelineStep
{
    private readonly IAspireManager _aspireManager;

    private IAsyncDisposable? _app;

    public StartAspirePipelineStep(IAspireManager aspireManager)
    {
        this._aspireManager = aspireManager;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        // No need to start aspire if there are no services to run
        if (state.Services.Count == 0)
        {
            return;
        }

        this._app = await this._aspireManager.StartAsync(cancellationToken);
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._app != null)
        {
            await this._app.DisposeAsync();
        }
    }
}
