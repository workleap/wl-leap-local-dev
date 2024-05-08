using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Immutable;
using System.Globalization;

namespace Leap.Cli.Aspire;
internal sealed class ExternalContainerResourceLifecycleHook(ILogger<ExternalContainerResourceLifecycleHook> logger, ResourceNotificationService notificationService, ResourceLoggerService loggerService)
    : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();

    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        foreach (var resource in appModel.Resources.OfType<ExternalContainerResource>())
        {
            this.StartTrackingExternalContainerLogs(resource, this._tokenSource.Token);
        }

        return Task.CompletedTask;
    }

    private void StartTrackingExternalContainerLogs(ExternalContainerResource resource, CancellationToken cancellationToken)
    {
        var resourceLogger = loggerService.GetLogger(resource);

        _ = Task.Run(async () =>
        {
            var client = new DockerClientConfiguration().CreateClient();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var container = await client.Containers.InspectContainerAsync(resource.ContainerNameOrId, cancellationToken);
                    var image = await client.Images.InspectImageAsync(container.Image, cancellationToken);

                    var status = container.State switch
                    {
                        { Running: true } => "Running",
                        { Restarting: true } => "Running",
                        { Paused: true } => "Paused",
                        _ => "Finished",
                    };

                    // Use "localhost" instead of "127.0.0.1" as the Dashboard doesn't support it
                    var urls = from port in container.NetworkSettings.Ports
                               let name = port.Key
                               from mapping in (port.Value ?? [])
                               select new UrlSnapshot(name, "localhost:" + mapping.HostPort, IsInternal: false);

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
                            new("container.args", (ImmutableArray<string>)[.. container.Args]),
                            new("container.ports", (ImmutableArray<int>)[.. ports]),
                            new("Image sha", image.ID),
                            ]),
                        EnvironmentVariables = [.. env],
                        Urls = [.. urls],
                    }));

                    var logParameters = new ContainerLogsParameters { Timestamps = true, Follow = true, ShowStderr = true, ShowStdout = true };
                    await client.Containers.GetContainerLogsAsync(resource.ContainerNameOrId, logParameters, cancellationToken,
                        new Progress<string>(line => resourceLogger.LogInformation("{StdOut}", line)));

                    await notificationService.PublishUpdateAsync(resource, state => (state with { State = "Finished" }));
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while updating container info");
                }
            }
        }, CancellationToken.None); // Do not use the cancellationToken as there is no await on this Task, so the exception would be unobserved
    }

    public async ValueTask DisposeAsync()
    {
        await this._tokenSource.CancelAsync();
        this._tokenSource.Dispose();
    }
}