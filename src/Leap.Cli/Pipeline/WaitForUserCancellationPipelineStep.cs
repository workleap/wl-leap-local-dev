using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WaitForUserCancellationPipelineStep : IPipelineStep
{
    private readonly ILogger _logger;

    public WaitForUserCancellationPipelineStep(ILogger<WaitForUserCancellationPipelineStep> logger)
    {
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
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