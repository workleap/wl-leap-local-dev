using System.Diagnostics;
using Leap.Cli.Model;
using Leap.Cli.Pipeline;

namespace Leap.Cli.Platform.Telemetry;

internal sealed class InstrumentedPipelineStep : IPipelineStep
{
    private readonly IPipelineStep _underlyingPipelineStep;
    private readonly ITelemetryHelper _telemetryHelper;

    public InstrumentedPipelineStep(IPipelineStep underlyingPipelineStep, ITelemetryHelper telemetryHelper)
    {
        this._underlyingPipelineStep = underlyingPipelineStep;
        this._telemetryHelper = telemetryHelper;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        using var activity = this.CreatePipelineStepActivity("start");
        await this._underlyingPipelineStep.StartAsync(state, cancellationToken);
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        using var activity = this.CreatePipelineStepActivity("stop");
        await this._underlyingPipelineStep.StopAsync(state, cancellationToken);
    }

    private Activity? CreatePipelineStepActivity(string displayNameSuffix)
    {
        var activity = this._telemetryHelper.StartChildActivity(TelemetryConstants.ActivityNames.PipelineStep, ActivityKind.Internal);

        if (activity != null)
        {
            activity.DisplayName = this._underlyingPipelineStep.GetType().Name + ' ' + displayNameSuffix;
        }

        return activity;
    }
}