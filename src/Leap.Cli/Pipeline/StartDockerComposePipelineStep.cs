using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class StartDockerComposePipelineStep : IPipelineStep
{
    private readonly IDockerComposeManager _dockerCompose;
    private readonly ILogger _logger;

    public StartDockerComposePipelineStep(IDockerComposeManager dockerCompose, ILogger<StartDockerComposePipelineStep> logger)
    {
        this._dockerCompose = dockerCompose;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._dockerCompose.Configuration.Services.Count == 0)
        {
            return;
        }

        this._logger.LogDebug("Creating docker-compose.yaml file...");
        await this._dockerCompose.WriteUpdatedDockerComposeFileAsync(cancellationToken);

        this._logger.LogInformation("Starting Docker services. This process might take some time if images need to be downloaded...");
        await this._dockerCompose.StartDockerComposeAsync(cancellationToken);

        this._logger.LogInformation("Docker services are up and running");
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}