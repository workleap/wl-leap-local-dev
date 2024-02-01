using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureDockerIsRunningPipelineStep : IPipelineStep
{
    private readonly IDockerComposeManager _dockerCompose;
    private readonly ILogger _logger;

    public EnsureDockerIsRunningPipelineStep(IDockerComposeManager dockerCompose, ILogger<EnsureLeapDirectoriesCreatedPipelineStep> logger)
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

        this._logger.LogDebug("Checking if Docker is running...");

        try
        {
            await this._dockerCompose.EnsureDockerIsRunningAsync(cancellationToken);
        }
        catch (LeapException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LeapException("An unexpected error occurred while checking if Docker is running: " + ex.Message, ex);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}