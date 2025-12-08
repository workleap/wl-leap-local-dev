using System.Diagnostics;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WaitForUserCancellationPipelineStep(
    ITelemetryHelper telemetryHelper,
    ILogger<WaitForUserCancellationPipelineStep> logger) : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var leapStartTimestamp = Stopwatch.GetTimestamp();
        TelemetryMeters.TrackLeapStartDuration(new TimeSpan(leapStartTimestamp - state.StartTime));
        telemetryHelper.StopRootActivity();

        logger.LogInformation("Press Ctrl+C to stop Leap");

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