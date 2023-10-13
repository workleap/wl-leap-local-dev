using Leap.Cli.Dependencies;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class AfterStartingDependenciesPipelineStep : IPipelineStep
{
    private readonly IDependencyHandler[] _handlers;

    public AfterStartingDependenciesPipelineStep(IEnumerable<IDependencyHandler> handlers)
    {
        this._handlers = handlers.ToArray();
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var tasks =
            from handler in this._handlers
            from dependency in state.Dependencies
            select handler.AfterStartAsync(dependency, cancellationToken);

        await Task.WhenAll(tasks);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}