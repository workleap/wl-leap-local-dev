using Leap.Cli.Dependencies;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class BeforeStartingDependenciesPipelineStep : IPipelineStep
{
    private readonly IDependencyHandler[] _handlers;

    public BeforeStartingDependenciesPipelineStep(IEnumerable<IDependencyHandler> handlers)
    {
        this._handlers = handlers.ToArray();
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var handler in this._handlers)
        {
            foreach (var dependency in state.Dependencies)
            {
                // Tasks are sequential because we can't afford to mix up the console output of each dependency handler
                await handler.BeforeStartAsync(dependency, cancellationToken);
            }
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}