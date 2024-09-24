using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Leap.Cli.Platform;

namespace Leap.Cli.Pipeline;

internal sealed class WireServicesAndDependenciesPipelineStep(
    IEnvironmentVariableManager environmentVariables,
    IConfigureDockerCompose configureDockerCompose,
    IHostsFileManager hostsFileManager,
    IAspireManager aspire) : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var orderedEnvironmentVariables = environmentVariables.EnvironmentVariables.OrderBy(x => x.Name, StringComparer.Ordinal);

        foreach (var (envvarName, envvarValue, scope) in orderedEnvironmentVariables)
        {
            foreach (var resource in aspire.Builder.Resources)
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

            foreach (var service in state.Services.Values)
            {
                if (scope == EnvironmentVariableScope.Container && configureDockerCompose.Configuration.Services.TryGetValue(service.ContainerName, out var dockerComposeServiceYaml))
                {
                    dockerComposeServiceYaml.Environment.TryAdd(envvarName, envvarValue);
                }
            }
        }

        // Allow dependencies running in Docker Compose to reach custom services using *.localhost domains
        var hosts = await hostsFileManager.GetAllCustomHostnamesAsync(cancellationToken);
        if (hosts != null)
        {
            var dockerComposeExtraHosts = hosts.Select(host => $"{host}:host-gateway").ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var dockerComposeServiceYaml in configureDockerCompose.Configuration.Services.Values)
            {
                dockerComposeServiceYaml.ExtraHosts.AddRange(dockerComposeExtraHosts);
            }
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
