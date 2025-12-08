using System.IO.Abstractions;
using System.Reflection;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class CreateFusionAuthConfigFilesPipelineStep(IFileSystem fileSystem)
    : IPipelineStep
{
    private static readonly string[] FusionAuthConfigFiles =
    [
        "Resources/kickstart.json",
        "Resources/nginx-ssl-reverse-proxy.conf"
    ];

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var configFile in FusionAuthConfigFiles)
        {
            await using var fileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(configFile);

            if (fileStream == null)
            {
                throw new LeapException($"Failed to load the FusionAuth configuration file {configFile}.");
            }

            await using var stream = fileSystem.File.Create(Path.Combine(Constants.FusionAuthDirectoryPath, configFile.Split('/')[1]));
            await fileStream.CopyToAsync(stream, cancellationToken);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}