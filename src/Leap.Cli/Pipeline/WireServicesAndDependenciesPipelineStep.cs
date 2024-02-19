using Leap.Cli.DockerCompose;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.ProcessCompose;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WireServicesAndDependenciesPipelineStep : IPipelineStep
{
    private static readonly HashSet<string> ExcludedDockerServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AzuriteDependency.DependencyType,
        EventGridDependency.DependencyType,
        MongoDependency.DependencyType,
        PostgresDependency.DependencyType,
        RedisDependency.DependencyType,
        SqlServerDependency.DependencyType,
    };

    private readonly IFeatureManager _featureManager;
    private readonly ILogger<WireServicesAndDependenciesPipelineStep> _logger;
    private readonly IEnvironmentVariableManager _environmentVariables;
    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureProcessCompose _processCompose;

    public WireServicesAndDependenciesPipelineStep(
        IFeatureManager featureManager,
        ILogger<WireServicesAndDependenciesPipelineStep> logger,
        IEnvironmentVariableManager environmentVariables,
        IConfigureDockerCompose dockerCompose,
        IConfigureProcessCompose processCompose)
    {
        this._featureManager = featureManager;
        this._logger = logger;
        this._environmentVariables = environmentVariables;
        this._dockerCompose = dockerCompose;
        this._processCompose = processCompose;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(WireServicesAndDependenciesPipelineStep), FeatureIdentifiers.LeapPhase2);
            return Task.CompletedTask;
        }

        var orderedEnvironmentVariables = this._environmentVariables.EnvironmentVariables.OrderBy(x => x.Name, StringComparer.Ordinal);

        foreach (var (envvarName, envvarValue, scope) in orderedEnvironmentVariables)
        {
            if (scope == EnvironmentVariableScope.Container)
            {
                foreach (var (serviceName, serviceYaml) in this._dockerCompose.Configuration.Services)
                {
                    if (!ExcludedDockerServices.Contains(serviceName))
                    {
                        serviceYaml.Environment.TryAdd(envvarName, envvarValue);
                    }
                }
            }
            else if (scope == EnvironmentVariableScope.Host)
            {
                foreach (var processYaml in this._processCompose.Configuration.Processes.Values)
                {
                    processYaml.Environment.TryAdd(envvarName, envvarValue);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
