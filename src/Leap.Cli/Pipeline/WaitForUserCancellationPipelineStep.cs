using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WaitForUserCancellationPipelineStep : IPipelineStep
{
    private readonly ITelemetryHelper _telemetryHelper;
    private readonly ILogger _logger;

    public WaitForUserCancellationPipelineStep(
        ITelemetryHelper telemetryHelper,
        ILogger<WaitForUserCancellationPipelineStep> logger)
    {
        this._telemetryHelper = telemetryHelper;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._telemetryHelper.StopRootActivity();

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
