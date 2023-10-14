using CliWrap;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Spectre.Console;

namespace Leap.Cli.Pipeline;

internal sealed class StartDockerComposePipelineStep : IPipelineStep
{
    private readonly IDockerComposeManager _dockerComposeManager;
    private readonly ICliWrapCommandExecutor _commandExecutor;
    private readonly IAnsiConsole _console;

    public StartDockerComposePipelineStep(IDockerComposeManager dockerComposeManager, ICliWrapCommandExecutor commandExecutor, IAnsiConsole console)
    {
        this._dockerComposeManager = dockerComposeManager;
        this._commandExecutor = commandExecutor;
        this._console = console;
    }

    public async Task<PipelineStepResult> StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        await this._dockerComposeManager.WriteUpdatedDockerComposeFileAsync(cancellationToken);

        // TODO "docker compose up" does throw if there's no services in the docker-compose.yml file, handle this case
        var command = new Command("docker")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(ConfigurationConstants.GeneratedDirectoryPath)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(this._console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(this._console.WriteLine))
            .WithArguments(new[] { "compose", "up", "--pull", "missing", "--remove-orphans", "--wait" });

        await this._console.Status().StartAsync("Starting Docker services...", async _ =>
        {
            var result = await this._commandExecutor.ExecuteAsync(command, cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"An error occurred while starting Docker services with '{command.TargetFilePath} {command.Arguments}'");
            }
        });

        this._console.MarkupLine("[green]Docker services are up and running[/]");
        return PipelineStepResult.Continue;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}