using Aspire.Hosting.ApplicationModel;
using Leap.Cli.Aspire;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class WireServicesAndDependenciesPipelineStep : IPipelineStep
{
    private readonly IEnvironmentVariableManager _environmentVariables;
    private readonly IAspireManager _aspire;

    public WireServicesAndDependenciesPipelineStep(
        IEnvironmentVariableManager environmentVariables,
        IAspireManager aspire)
    {
        this._environmentVariables = environmentVariables;
        this._aspire = aspire;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
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
