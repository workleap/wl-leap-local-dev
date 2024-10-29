using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureDockerIsRunningPipelineStep(
    IDockerComposeManager dockerCompose,
    ILogger<EnsureDockerIsRunningPipelineStep> logger) : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var requiresDocker = dockerCompose.Configuration.Services.Count > 0 || state.Services.Values.Any(x => x.ActiveRunner is DockerRunner);
        if (!requiresDocker)
        {
            return;
        }

        logger.LogDebug("Checking if Docker is running...");

        try
        {
            await dockerCompose.EnsureDockerIsRunningAsync(cancellationToken);
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