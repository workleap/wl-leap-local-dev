using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class AddDependenciesToAspireDashboard(IAspireManager aspireManager, IConfigureDockerCompose configureDockerCompose) : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var service in configureDockerCompose.Configuration.Services)
        {
            if (service.Value.ContainerName is not null)
            {
                aspireManager.Builder.AddExternalContainer(service.Key, service.Value.ContainerName);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}