using Leap.Cli.DockerCompose;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class StartDockerComposePipelineStep : IPipelineStep
{
    private readonly IDockerComposeManager _dockerComposeManager;

    public StartDockerComposePipelineStep(IDockerComposeManager dockerComposeManager)
    {
        this._dockerComposeManager = dockerComposeManager;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return this._dockerComposeManager.WriteUpdatedDockerComposeFileAsync(cancellationToken);

        // TODO run docker compose up with the right arguments
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}