using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Leap.Cli.Aspire;

// Task management inspired by
// https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dashboard/DashboardLifecycleHook.cs
internal sealed class ExternalContainerResourceLifecycleHook(ILogger<ExternalContainerResourceLifecycleHook> logger, ResourceNotificationService notificationService, ResourceLoggerService loggerService)
    : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();
    private Task? _trackTask;

    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        this._trackTask = this.TrackExternalContainers(appModel, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task TrackExternalContainers(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        List<Task> tasks = [];

        foreach (var resource in appModel.Resources.OfType<ExternalContainerResource>())
        {
            tasks.Add(this.TrackExternalContainer(resource, cancellationToken));
        }

        // The watch task should already be logging exceptions, so we don't need to log them here.
        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task TrackExternalContainer(ExternalContainerResource resource, CancellationToken cancellationToken)
    {
        var resourceLogger = loggerService.GetLogger(resource);

        using var client = new DockerClientConfiguration().CreateClient();

        DateTimeOffset? since = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var container = await client.Containers.InspectContainerAsync(resource.ContainerNameOrId, cancellationToken);

                var image = await client.Images.InspectImageAsync(container.Image, cancellationToken);

                var status = container.State switch
                {
                    { Running: true } or { Restarting: true } => new ResourceStateSnapshot("Running", KnownResourceStateStyles.Success),
                    { Paused: true } => new ResourceStateSnapshot("Paused", KnownResourceStateStyles.Info),
                    { ExitCode: not 0 } => new ResourceStateSnapshot("Exited", KnownResourceStateStyles.Error),
                    _ => new ResourceStateSnapshot("Finished", KnownResourceStateStyles.Info),
                };

                var ports = from port in container.NetworkSettings.Ports
                            from mapping in (port.Value ?? [])
                            select int.Parse(mapping.HostPort, NumberStyles.None, CultureInfo.InvariantCulture);

                var env = container.Config.Env.Select(env => env.Split('=', count: 2)).Select(parts => new EnvironmentVariableSnapshot(parts[0], parts[1], IsFromSpec: true));

                await notificationService.PublishUpdateAsync(resource, state => (state with
                {
                    State = status,
                    CreationTimeStamp = container.Created,
                    ExitCode = container.State.FinishedAt is null ? (int)container.State.ExitCode : null,
                    Properties = ImmutableArray.Create<ResourcePropertySnapshot>([
                        new("container.id", container.ID),
                        new("container.image", container.Config.Image),
                        new("container.command", string.Join(' ', container.Config.Cmd ?? [])),
                        new("container.args", (ImmutableArray<string>) [.. container.Args]),
                        new("container.ports", (ImmutableArray<int>) [.. ports]),
                        new("Image sha", image.ID),
                    ]),
                    EnvironmentVariables = [.. env],
                }));

                var logParameters = new ContainerLogsParameters
                {
                    Timestamps = false,
                    Follow = true,
                    ShowStderr = true,
                    ShowStdout = true,
                    Since = since?.ToUnixTimeSeconds().ToString()
                };

                // TODO We noticed that the ordering of logs is somehow not guaranteed, we might want to address this in the future.
                await client.Containers.GetContainerLogsAsync(resource.ContainerNameOrId, logParameters, cancellationToken, new Progress<string>(line =>
                {
                    if (TryRemoveNonPrintableMultiplexedDockerLogHeader(line, out var remainder))
                    {
                        line = remainder;
                    }

                    resourceLogger.LogInformation("{StdOut}", line);
                }));

                await notificationService.PublishUpdateAsync(resource, state => state with { State = status });
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                // Application is shutting down
                break;
            }
            catch
            {
                // Ignored as we do not want to flood the user console logs with errors that are not actionable.
            }

#pragma warning disable RS0030 // "--since" accepts local datetimes (https://docs.docker.com/reference/cli/docker/container/logs/)
            since = DateTimeOffset.Now;
#pragma warning restore RS0030

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

    public async ValueTask DisposeAsync()
    {
        await this._tokenSource.CancelAsync();

        if (this._trackTask != null)
        {
            try
            {
                await this._trackTask;
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

        this._tokenSource.Dispose();
    }
}