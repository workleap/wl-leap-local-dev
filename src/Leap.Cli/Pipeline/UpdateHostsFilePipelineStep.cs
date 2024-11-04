using Leap.Cli.Commands;
using Leap.Cli.Dependencies;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class UpdateHostsFilePipelineStep : IPipelineStep
{
    private readonly IPlatformHelper _platformHelper;
    private readonly IHostsFileManager _hostsFileManager;
    private readonly ILogger _logger;

    public UpdateHostsFilePipelineStep(
        IPlatformHelper platformHelper,
        IHostsFileManager hostsFileManager,
        ILogger<UpdateHostsFilePipelineStep> logger)
    {
        this._platformHelper = platformHelper;
        this._hostsFileManager = hostsFileManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var existingHostnames = await this._hostsFileManager.GetLeapManagedHostnamesAsync(cancellationToken);
        if (existingHostnames == null)
        {
            return;
        }

        var requiredHostnames = state.Services.Values
            .Where(x => !x.Ingress.Host.IsLocalhost)
            .Select(x => x.Ingress.Host.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Allows MongoDB replica set connection string to resolve the name of the Docker Compose service used by the replica set member
        // https://dba.stackexchange.com/a/78550/83022
        requiredHostnames.Add(MongoDependencyHandler.ServiceName);

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
            await this._hostsFileManager.UpdateLeapManagedHostnamesAsync(finalUniqueHostnames, cancellationToken);
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
