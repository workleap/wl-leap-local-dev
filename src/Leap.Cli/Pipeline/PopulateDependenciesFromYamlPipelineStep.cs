using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Dependencies;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateDependenciesFromYamlPipelineStep : IPipelineStep
{
    private readonly ILeapYamlAccessor _leapYamlAccessor;
    private readonly IEnumerable<IDependencyYamlHandler> _dependencyYamlHandlers;

    public PopulateDependenciesFromYamlPipelineStep(ILeapYamlAccessor leapYamlAccessor, IEnumerable<IDependencyYamlHandler> dependencyYamlHandlers)
    {
        this._leapYamlAccessor = leapYamlAccessor;
        this._dependencyYamlHandlers = dependencyYamlHandlers;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var leapConfigs = await this._leapYamlAccessor.GetAllAsync(cancellationToken);

        var dependenciesYaml = leapConfigs.SelectMany(x => x.Content.Dependencies?.OfType<DependencyYaml>() ?? Enumerable.Empty<DependencyYaml>()).GroupBy(x => x.Type);

        foreach (var dependencyGroup in dependenciesYaml)
        {
            var dependencyHandler = this._dependencyYamlHandlers.FirstOrDefault(handler => handler.CanHandle(dependencyGroup.Key));

            if (dependencyHandler == null)
            {
                throw new InvalidOperationException($"No yaml dependency handler found for dependency type {dependencyGroup.Key}");
            }

            var dependencyYaml = dependencyGroup.Aggregate(new DependencyYaml(), dependencyHandler.Merge);
            var dependency = dependencyHandler.ToDependencyModel(dependencyYaml);

            if (!state.Dependencies.Contains(dependency))
            {
                state.Dependencies.Add(dependency);
            }

            this.PopulateDependencies(dependency, state);
        }
    }

    private void PopulateDependencies(Dependency dependency, ApplicationState state)
    {
        foreach (var childDependency in dependency.Dependencies)
        {
            // TODO:
            // This will probably cause problems when it comes to actually merging dependencies that have settings like azurite.
            // Currently, these merges are performed by the yaml handlers, but dependencies not declared by yaml, but rather
            // parent dependencies don't rely on this merge process before being constructed.
            if (!state.Dependencies.Contains(childDependency))
            {
                state.Dependencies.Add(childDependency);
                // Only process children the first time this is added to avoid infinite recursion
                this.PopulateDependencies(childDependency, state);
            }
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}