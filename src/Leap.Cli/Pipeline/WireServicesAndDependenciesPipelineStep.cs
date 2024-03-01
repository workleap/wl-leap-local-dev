using Leap.Cli.Aspire;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WireServicesAndDependenciesPipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<WireServicesAndDependenciesPipelineStep> _logger;
    private readonly IEnvironmentVariableManager _environmentVariables;
    private readonly IAspireManager _aspire;

    public WireServicesAndDependenciesPipelineStep(
        IFeatureManager featureManager,
        ILogger<WireServicesAndDependenciesPipelineStep> logger,
        IEnvironmentVariableManager environmentVariables,
        IAspireManager aspire)
    {
        this._featureManager = featureManager;
        this._logger = logger;
        this._environmentVariables = environmentVariables;
        this._aspire = aspire;
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
            foreach (var resource in this._aspire.Builder.Resources)
            {
                var envCallbackAnnotation = new EnvironmentCallbackAnnotation(context =>
                {
                    context.EnvironmentVariables.TryAdd(envvarName, envvarValue);
                });

                if (resource is ExecutableResource executable && scope == EnvironmentVariableScope.Host)
                {
                    executable.Annotations.Add(envCallbackAnnotation);
                }
                else if (resource is ContainerResource container && scope == EnvironmentVariableScope.Container)
                {
                    container.Annotations.Add(envCallbackAnnotation);
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
