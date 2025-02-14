using CliWrap;
using CliWrap.Buffered;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class LoginAzureAcrPipelineStep(
    ICliWrap cliWrap,
    LeapConfigManager leapConfigManager,
    ILogger<LoginAzureAcrPipelineStep> logger) : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (leapConfigManager.RemoteEnvironmentName is not null)
        {
            return;
        }

        var acrNames = state.Services.Values
            .Select(x => x.ActiveRunner)
            .OfType<DockerRunner>()
            .Select(x => new DockerComposeImageName(x.ImageAndTag).GetAzureContainerRegistryName())
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var acrName in acrNames)
        {
            await this.LoginToAzureAcrAsync(acrName, cancellationToken);
        }

        await this.LoginToAzureAcrAsync(Constants.FusionAuthProvisioningAcrName, cancellationToken);
    }

    private async Task LoginToAzureAcrAsync(string acrName, CancellationToken cancellationToken)
    {
        logger.LogDebug("Logging in to Azure container registry '{AcrName}'", acrName);
        var command = new Command("az").WithArguments(["acr", "login", "--name", acrName]).WithValidation(CommandResultValidation.None);

        BufferedCommandResult result;
        try
        {
            result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);
        }
        catch (Exception)
        {
            throw new LeapException($"Error logging in to Azure container registry '{acrName}'. Verify that Azure CLI is installed.");
        }

        if (result.ExitCode != 0)
        {
            logger.LogWarning("Error logging in to Azure container registry '{AcrName}'. '{Command.TargetFilePath} {Command.Arguments}' returned {Result.ExitCode}.", acrName, command.TargetFilePath, command.Arguments, result.ExitCode);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}