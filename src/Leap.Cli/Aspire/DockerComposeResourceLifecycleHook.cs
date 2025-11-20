using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Docker.DotNet;
using Docker.DotNet.Models;
using Leap.Cli.DockerCompose;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

// Task management inspired by
// https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dashboard/DashboardLifecycleHook.cs
internal sealed class DockerComposeResourceLifecycleHook(
    ILogger<DockerComposeResourceLifecycleHook> logger,
    IDockerComposeManager dockerComposeManager,
    ResourceNotificationService notificationService,
    ResourceLoggerService loggerService)
    : IDistributedApplicationEventingSubscriber, IAsyncDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly DockerClient _dockerClient = new DockerClientConfiguration().CreateClient();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _getContainerLogsSince = new(StringComparer.Ordinal);
    private Task? _mainTask;

    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(this.BeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task BeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
    {
        this._mainTask = this.StartAndWatchContainersAsync(@event.Model, this._tokenSource.Token);

        return Task.CompletedTask;
    }

    private async Task StartAndWatchContainersAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        List<Task> tasks = [];

        foreach (var resource in appModel.Resources.OfType<DockerComposeResource>())
        {
            this.AddStopContainerCommand(resource);
            this.AddStartContainerCommand(resource);
            tasks.Add(this.StartAndWatchContainerAsync(resource, cancellationToken));
        }

        // The watch task should already be logging exceptions, so we don't need to log them here.
        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task StartAndWatchContainerAsync(DockerComposeResource resource, CancellationToken cancellationToken)
    {
        await notificationService.WaitForDependenciesAsync(resource, cancellationToken);

        // Removing the potential "Waiting" state that was set by the WaitForDependenciesAsync.
        await notificationService.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = resource.InitialState,
        });

        var resourceLogger = loggerService.GetLogger(resource);
        this._getContainerLogsSince[resource.ContainerName] = DateTimeOffset.Now;

        if (!resource.HasAnnotationOfType<ExplicitStartupAnnotation>())
        {
            await this.StartContainerAsync(resource, resourceLogger, cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await this.WatchContainerAsync(resource, resourceLogger, cancellationToken);
        }
    }

    private async Task StartContainerAsync(DockerComposeResource resource, ILogger resourceLogger, CancellationToken cancellationToken)
    {
        try
        {
            await dockerComposeManager.StartDockerComposeServiceAsync(resource.Name, resourceLogger, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down
        }
        catch
        {
            await notificationService.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = KnownResourceStates.FailedToStart,
                StopTimeStamp = DateTime.Now,
            });
        }
    }

    private async Task WatchContainerAsync(DockerComposeResource resource, ILogger resourceLogger, CancellationToken cancellationToken)
    {
        try
        {
            await this.SynchronizeResourceSnapshotWithContainerAsync(resource, cancellationToken);
            await this.WatchContainerLogsUntilStoppedAsync(resource, resourceLogger, cancellationToken);
            await this.SynchronizeResourceSnapshotWithContainerAsync(resource, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application is shutting down
            return;
        }
        catch
        {
            // Ignored as we do not want to flood the user console logs with errors that are not actionable.
        }

        try
        {
            // Wait for a few seconds before checking the container again.
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down
        }
    }

    private async Task SynchronizeResourceSnapshotWithContainerAsync(DockerComposeResource resource, CancellationToken cancellationToken)
    {
        // https://docs.docker.com/reference/api/engine/version/v1.47/#tag/Container/operation/ContainerInspect
        var container = await this._dockerClient.Containers.InspectContainerAsync(resource.ContainerName, cancellationToken);

        var status = container.State.Status switch
        {
            "created" => KnownResourceStates.Starting,
            "restarting" => KnownResourceStates.Starting,
            "running" => KnownResourceStates.Running,
            "paused" => KnownResourceStates.Running,
            "removing" => KnownResourceStates.Stopping,
            "exited" => container.State.ExitCode == 0 ? KnownResourceStates.Finished : KnownResourceStates.Exited,
            "dead" => KnownResourceStates.Exited,
            _ => new ResourceStateSnapshot("Unknown", KnownResourceStateStyles.Info), // Should not happen, we covered all known states.
        };

        // Examples: "2024-10-31T13:16:45.067731016Z" or "0001-01-01T00:00:00Z"
        DateTime? startedAt = DateTimeOffset.TryParse(container.State.StartedAt, out var parsedStartedAt) && parsedStartedAt != DateTimeOffset.MinValue ? parsedStartedAt.DateTime.ToLocalTime() : null;
        DateTime? finishedAt = DateTimeOffset.TryParse(container.State.FinishedAt, out var parsedFinishedAt) && parsedFinishedAt != DateTimeOffset.MinValue ? parsedFinishedAt.DateTime.ToLocalTime() : null;

        var ports = container.NetworkSettings.Ports.SelectMany(x => x.Value ?? [])
            .Select(x => int.Parse(x.HostPort, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToImmutableArray();

        var environmentVariables = container.Config.Env.Select(env => env.Split('=', count: 2))
            .Select(parts => new EnvironmentVariableSnapshot(parts[0], parts[1], IsFromSpec: true))
            .ToImmutableArray();

        var isInTerminalState = KnownResourceStates.TerminalStates.Contains(status?.Text);

        await notificationService.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = status,
            CreationTimeStamp = container.Created,
            StartTimeStamp = startedAt,
            StopTimeStamp = isInTerminalState ? finishedAt : null,
            ExitCode = isInTerminalState ? (int)container.State.ExitCode : null,
            EnvironmentVariables = environmentVariables,
            Urls = isInTerminalState ? [] : [.. resource.Urls.Select((url, idx) => new UrlSnapshot(EndpointNameHelper.GetLocalhostEndpointName(idx), url, IsInternal: false))],
            Properties =
            [
                // https://github.com/dotnet/aspire/blob/v8.2.2/src/Shared/Model/KnownProperties.cs#L30-L34
                new ResourcePropertySnapshot("container.id", container.ID),
                new ResourcePropertySnapshot("container.image", container.Config.Image),
                new ResourcePropertySnapshot("container.command", string.Join(' ', container.Config.Cmd ?? [])),
                new ResourcePropertySnapshot("container.args", container.Args.ToImmutableArray()),
                new ResourcePropertySnapshot("container.ports", ports),
            ],
        });
    }

    private async Task WatchContainerLogsUntilStoppedAsync(DockerComposeResource resource, ILogger resourceLogger, CancellationToken cancellationToken)
    {
        var getLogsParameters = new ContainerLogsParameters
        {
            Timestamps = false,
            Follow = true,
            ShowStderr = true,
            ShowStdout = true,
            Since = this._getContainerLogsSince[resource.ContainerName].ToUnixTimeSeconds().ToString(),
        };

        try
        {
            await this._dockerClient.Containers.GetContainerLogsAsync(resource.ContainerName, getLogsParameters, cancellationToken, new Progress<string>(line =>
            {
                if (TryRemoveNonPrintableMultiplexedDockerLogHeader(line, out var remainder))
                {
                    line = remainder;
                }

                resourceLogger.LogInformation("{StdOut}", line);
            }));
        }
        finally
        {
            this._getContainerLogsSince[resource.ContainerName] = DateTimeOffset.Now;
        }
    }

    private static bool TryRemoveNonPrintableMultiplexedDockerLogHeader(string line, [NotNullWhen(true)] out string? remainder)
    {
        // Docker logs for non-TTY containers have a non-printable 8-byte header to distinguish the originating stream (stdin, stdout, stderr):
        // 1 byte stream type, 3 bytes padding, 4 bytes payload size. See:
        // https://docs.docker.com/reference/api/engine/version/v1.26/#tag/Container/operation/ContainerAttach
        if (line is ['\u0000' or '\u0001' or '\u0002', '\u0000', '\u0000', '\u0000', _, _, _, _, .. var rest])
        {
            remainder = rest;
            return true;
        }

        remainder = null;
        return false;
    }

    private void AddStopContainerCommand(DockerComposeResource resource)
    {
        var command = new ResourceCommandAnnotation(
            name: "stop-container",
            displayName: "Stop",
            updateState: context => !IsStoppedOrStopping(context.ResourceSnapshot.State?.Text) ? ResourceCommandState.Enabled : ResourceCommandState.Hidden,
            executeCommand: async context =>
            {
                try
                {
                    await dockerComposeManager.StopDockerComposeServiceAsync(resource.Name, loggerService.GetLogger(resource), context.CancellationToken);
                }
                catch (Exception ex)
                {
                    return new ExecuteCommandResult
                    {
                        ErrorMessage = "An error occurred while trying to stop the container: " + ex.Message,
                        Success = false
                    };
                }

                TelemetryMeters.TrackDockerResourceCommand(resource.Name);
                return CommandResults.Success();
            },
            displayDescription: null,
            parameter: null,
            confirmationMessage: null,
            iconName: "Stop",
            iconVariant: IconVariant.Filled,
            isHighlighted: true);

        resource.Annotations.Add(command);
    }

    private void AddStartContainerCommand(DockerComposeResource resource)
    {
        var command = new ResourceCommandAnnotation(
            name: "start-container",
            displayName: "Start",
            updateState: context => IsStoppedOrStopping(context.ResourceSnapshot.State?.Text) ? ResourceCommandState.Enabled : ResourceCommandState.Hidden,
            executeCommand: async context =>
            {
                try
                {
                    await dockerComposeManager.StartDockerComposeServiceAsync(resource.Name, loggerService.GetLogger(resource), context.CancellationToken);
                }
                catch (Exception ex)
                {
                    return new ExecuteCommandResult
                    {
                        ErrorMessage = "An error occurred while trying to start the container: " + ex.Message,
                        Success = false
                    };
                }

                TelemetryMeters.TrackDockerResourceCommand(resource.Name);
                return CommandResults.Success();
            },
            displayDescription: null,
            parameter: null,
            confirmationMessage: null,
            iconName: "Play",
            iconVariant: IconVariant.Filled,
            isHighlighted: true);

        resource.Annotations.Add(command);
    }

    public async ValueTask DisposeAsync()
    {
        await this._tokenSource.CancelAsync();

        if (this._mainTask != null)
        {
            try
            {
                await this._mainTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when the application is shutting down.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while tracking external containers.");
            }
        }

        this._dockerClient.Dispose();
        this._tokenSource.Dispose();
    }

    // https://github.com/dotnet/aspire/blob/34d6aabf330dd4ea0bf69fca138c8c1ba1250fce/src/Aspire.Hosting/ApplicationModel/CommandsConfigurationExtensions.cs#L115
    private static bool IsStoppedOrStopping(string? state) => KnownResourceStates.TerminalStates.Contains(state) || state == KnownResourceStates.Stopping;
}