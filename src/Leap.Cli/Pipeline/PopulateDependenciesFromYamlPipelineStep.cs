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

            state.Dependencies.Add(dependency);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}