using System.Diagnostics;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Pipeline;

internal sealed class TrackLeapRunDurationPipelineStep : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        state.StartTime = Stopwatch.GetTimestamp();
        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var runtime = Stopwatch.GetTimestamp();
        TelemetryMeters.TrackLeapRunDuration(new TimeSpan(runtime - state.StartTime));

        return Task.CompletedTask;
    }
}