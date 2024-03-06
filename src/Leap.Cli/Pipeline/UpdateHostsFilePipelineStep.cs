using Leap.Cli.Commands;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class UpdateHostsFilePipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly IPlatformHelper _platformHelper;
    private readonly IHostsFileManager _hostsFileManager;
    private readonly ILogger _logger;

    public UpdateHostsFilePipelineStep(
        IFeatureManager featureManager,
        IPlatformHelper platformHelper,
        IHostsFileManager hostsFileManager,
        ILogger<UpdateHostsFilePipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._platformHelper = platformHelper;
        this._hostsFileManager = hostsFileManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(UpdateHostsFilePipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        var existingHostnames = await this._hostsFileManager.GetHostnamesAsync(cancellationToken);
        if (existingHostnames == null)
        {
            return;
        }

        var requiredHostnames = state.Services.Values.Select(x => x.Ingress.Host).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingHostnames = requiredHostnames.Except(existingHostnames, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missingHostnames.Length == 0)
        {
            this._logger.LogTrace("Hosts file is already up to date");
            return;
        }

        var finalUniqueHostnames = existingHostnames.Concat(requiredHostnames).ToArray();
        this._logger.LogTrace("Updating hosts file to add the following hostnames: {Hostnames}", string.Join(", ", missingHostnames));

        if (this._platformHelper.IsCurrentProcessElevated)
        {
            await this._hostsFileManager.UpdateHostnamesAsync(finalUniqueHostnames, cancellationToken);
        }
        else
        {
            this._logger.LogWarning("Please accept to elevate the process to update the hosts file");

            var leapArgs = new[]
            {
                UpdateHostsFileCommand.CommandName,
                string.Join(UpdateHostsFileCommand.HostSeparator, finalUniqueHostnames),
            };

            await this._platformHelper.StartLeapElevatedAsync(leapArgs, cancellationToken);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
