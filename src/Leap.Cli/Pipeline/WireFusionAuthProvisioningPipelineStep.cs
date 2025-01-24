using Leap.Cli.Aspire;
using Leap.Cli.Dependencies;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class WireFusionAuthProvisioningPipelineStep(
    IEnvironmentVariableManager environmentVariables,
    IConfigureDockerCompose configureDockerCompose,
    IAspireManager aspire) : IPipelineStep
{
    /*
     * We need those two environment variables which are not injected to dependencies
     * in order for our container to access the Azure credentials.
     */
    private readonly string[] _envVarNames = ["IDENTITY_ENDPOINT", "IMDS_ENDPOINT"];

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var fusionAuthDep = state.Dependencies.FirstOrDefault(x => x.Name == FusionAuthDependencyYaml.YamlDiscriminator);

        if (fusionAuthDep == null)
        {
            return Task.CompletedTask;
        }

        var faProvisioningAspireResource = aspire.Builder.Resources.FirstOrDefault(x => x.Name == Constants.FusionAuthProvisioningServiceName)!;

        if (configureDockerCompose.Configuration.Services.TryGetValue(Constants.FusionAuthProvisioningServiceName, out var fusionAuthProvisioning))
        {
            foreach (var envVarName in this._envVarNames)
            {
                var envVar = environmentVariables.EnvironmentVariables.FirstOrDefault(
                    x =>
                        x.Name == envVarName &&
                        x.Scope == EnvironmentVariableScope.Container);

                if (envVar != null)
                {
                    var envCallbackAnnotation = new EnvironmentCallbackAnnotation(context =>
                    {
                        context.EnvironmentVariables.TryAdd(envVar.Name, envVar.Value);
                    });

                    faProvisioningAspireResource.Annotations.Add(envCallbackAnnotation);
                    fusionAuthProvisioning.Environment.TryAdd(envVar.Name, envVar.Value);
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