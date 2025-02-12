using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Dependencies;
using Leap.Cli.Dependencies.Azurite;
using Leap.Cli.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateDependenciesFromYamlPipelineStep(ILeapYamlAccessor leapYamlAccessor, IServiceProvider serviceProvider, LeapConfigManager leapConfigManager, ILogger<PopulateDependenciesFromYamlPipelineStep> logger) : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (leapConfigManager.RemoteEnvironmentName is not null)
        {
            logger.LogInformation("Remote-env option is set, skipping dependencies..");
            return;
        }

        var leapConfigs = await leapYamlAccessor.GetAllAsync(cancellationToken);

        var allDependenciesYaml = leapConfigs.SelectMany(x => x.Content.Dependencies?.OfType<DependencyYaml>() ?? []).ToArray();

        this.PopulateDependencies<AzuriteDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<EventGridDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<MongoDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<PostgresDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<RedisDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<FusionAuthDependencyYaml>(state, allDependenciesYaml);
        this.PopulateDependencies<SqlServerDependencyYaml>(state, allDependenciesYaml);
    }

    private void PopulateDependencies<TYaml>(ApplicationState state, IEnumerable<DependencyYaml> allDependenciesYaml)
        where TYaml : DependencyYaml, new()
    {
        var typedDependenciesYaml = allDependenciesYaml.OfType<TYaml>().ToArray();
        if (typedDependenciesYaml.Length == 0)
        {
            return;
        }

        var dependencyHandler = serviceProvider.GetRequiredService<IDependencyYamlHandler<TYaml>>();

        var aggregatedDependencyYaml = typedDependenciesYaml.Aggregate(new TYaml(), dependencyHandler.Merge);
        var dependency = dependencyHandler.ToDependencyModel(aggregatedDependencyYaml);

        state.Dependencies.Add(dependency);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}