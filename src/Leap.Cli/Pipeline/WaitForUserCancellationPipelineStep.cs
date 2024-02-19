using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WaitForUserCancellationPipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger _logger;

    public WaitForUserCancellationPipelineStep(
        IFeatureManager featureManager,
        ILogger<WaitForUserCancellationPipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(WireServicesAndDependenciesPipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        // There's no need to wait for user cancellation if there's no service to run
        if (state.Services.Count == 0)
        {
            return;
        }

        this._logger.LogInformation("Press Ctrl+C to stop Leap");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cancellationRegistration = cancellationToken.Register(tcs.SetResult);

        await tcs.Task;

        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
