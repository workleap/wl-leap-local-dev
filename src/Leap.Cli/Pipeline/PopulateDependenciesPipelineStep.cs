using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateDependenciesPipelineStep : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        // TODO read dependencies from the YAML files and populate the ApplicationState
        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}