using Leap.Cli.Aspire;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class StartAspirePipelineStep : IPipelineStep
{
    private readonly IAspireManager _aspireManager;
    private readonly IFeatureManager _featureManager;
    private readonly ILogger _logger;

    private IAsyncDisposable? _app;

    public StartAspirePipelineStep(IAspireManager aspireManager, IFeatureManager featureManager, ILogger<StartAspirePipelineStep> logger)
    {
        this._aspireManager = aspireManager;
        this._featureManager = featureManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(StartAspirePipelineStep), FeatureIdentifiers.LeapPhase2);
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
