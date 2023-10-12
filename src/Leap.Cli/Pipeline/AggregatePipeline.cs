using System.Runtime.ExceptionServices;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class AggregatePipeline : IPipelineStep
{
    private readonly IPipelineStep[] _pipelineSteps;

    public AggregatePipeline(IEnumerable<IPipelineStep> pipelineSteps)
    {
        this._pipelineSteps = pipelineSteps.ToArray();
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var step in this._pipelineSteps)
        {
            await step.StartAsync(state, cancellationToken);
        }
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (var step in this._pipelineSteps.Reverse())
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