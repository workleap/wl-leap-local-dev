using System.Runtime.ExceptionServices;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class LeapPipeline
{
    private readonly IPipelineStep[] _steps;

    public LeapPipeline(IEnumerable<IPipelineStep> steps)
    {
        this._steps = steps.ToArray();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var state = new ApplicationState();

        try
        {
            await this.StartAsync(state, cancellationToken);
        }
        finally
        {
            // TODO this cancellation token is invalid if the user already pressed CTRL+C, we need another one with a graceful shutdown timeout
            await this.StopAsync(state, cancellationToken);
        }
    }

    private async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var step in this._steps)
        {
            var result = await step.StartAsync(state, cancellationToken);

            if (result == PipelineStepResult.Stop)
            {
                return;
            }
        }
    }

    private async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (var step in this._steps.Reverse())
        {
            try
            {
                await step.StopAsync(state, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }
        else if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}