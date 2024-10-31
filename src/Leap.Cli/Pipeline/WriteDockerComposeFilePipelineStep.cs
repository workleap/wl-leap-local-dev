using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class WriteDockerComposeFilePipelineStep(IDockerComposeManager dockerCompose, ILogger<WriteDockerComposeFilePipelineStep> logger) : IPipelineStep
{
    private readonly ILogger _logger = logger;

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (dockerCompose.Configuration.Services.Count == 0)
        {
            return;
        }

        this._logger.LogDebug("Writing docker-compose.yaml file...");
        await dockerCompose.WriteUpdatedDockerComposeFileAsync(cancellationToken);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}