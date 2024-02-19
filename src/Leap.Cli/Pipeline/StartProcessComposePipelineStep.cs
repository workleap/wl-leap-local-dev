using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.ProcessCompose;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class StartProcessComposePipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly IProcessComposeManager _processCompose;
    private readonly ILogger _logger;

    private Task? _processComposeUpTask;

    public StartProcessComposePipelineStep(
        IFeatureManager featureManager,
        IProcessComposeManager processCompose,
        ILogger<StartProcessComposePipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._processCompose = processCompose;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(StartProcessComposePipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        if (this._processCompose.Configuration.Processes.Count == 0)
        {
            return;
        }

        this._logger.LogDebug("Ensure Process Compose binary exists...");
        await this._processCompose.EnsureProcessComposeExecutableExistsAsync(cancellationToken);

        this._logger.LogDebug("Creating process-compose.yaml file...");
        await this._processCompose.WriteUpdatedProcessComposeFileAsync(cancellationToken);

        this._logger.LogInformation("Starting processes...");
        this._processComposeUpTask = this._processCompose.StartProcessComposeAsync(cancellationToken);
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._processComposeUpTask != null)
        {
            await this._processComposeUpTask;
        }
    }
}
