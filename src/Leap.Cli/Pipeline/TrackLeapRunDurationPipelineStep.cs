using System.Diagnostics;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Pipeline;

internal sealed class TrackLeapRunDurationPipelineStep : IPipelineStep
{
    private readonly Stopwatch _stopwatch;

    public TrackLeapRunDurationPipelineStep()
    {
        this._stopwatch = new Stopwatch();
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._stopwatch.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._stopwatch.Stop();
        var runTime = this._stopwatch.Elapsed;
        TelemetryMeters.TrackLeapRunDuration(runTime);

        return Task.CompletedTask;
    }
}
