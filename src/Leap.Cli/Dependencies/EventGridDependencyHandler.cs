using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Dependencies;

internal sealed class EventGridDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    ILogger<EventGridDependencyHandler> logger,
    IAspireManager aspire)
    : DependencyHandler<EventGridDependency>, IDisposable
{
    private const int EventGridPort = 6500;

    private const string ServiceName = EventGridDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-eventgrid";

    private static readonly string EventGridHostUrl = $"https://127.0.0.1:{EventGridPort}";
    private static readonly string EventGridContainerUrl = $"https://host.docker.internal:{EventGridPort}";

    private readonly SemaphoreSlim _generatedEventGridSettingsFileWriteLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

    private FileSystemWatcher? _userEventGridSettingsFileWatcher;

    protected override async Task HandleAsync(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackEventGridStart();
        await this.EnsureEventGridSettingsFilesExists(dependency, cancellationToken);
        this.StartWatchingUserEventGridSettings(dependency, cancellationToken);
        ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        aspire.Builder.AddDockerComposeResource(new DockerComposeResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [EventGridHostUrl]
        });
    }

    private async Task EnsureEventGridSettingsFilesExists(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        // When mounting a file that does not exists, Docker creates an empty directory on the host. Make sure to delete it if this ever happens.
        EnsureFileIsNotDirectory(Constants.UserEventGridSettingsFilePath);
        EnsureFileIsNotDirectory(Constants.GeneratedEventGridSettingsFilePath);

        if (!File.Exists(Constants.UserEventGridSettingsFilePath))
        {
            try
            {
                await using var stream = new FileStream(Constants.UserEventGridSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, new EventGridSettings(), EventGridSettingsSourceGenerationContext.Default.EventGridSettings, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "An error occurred while writing an empty Event Grid settings file at {EventGridSettingsFilePath}. You may create it yourself by following our documentation: https://github.com/workleap/wl-eventgrid-emulator. {ExceptionMessage}",
                    Constants.UserEventGridSettingsFilePath, ex.Message);
            }
        }

        await this.SyncGeneratedEventGridSettingsAsync(dependency, cancellationToken);
    }

    private static void EnsureFileIsNotDirectory(string filePath)
    {
        try
        {
            Directory.Delete(filePath, recursive: true);
        }
        catch
        {
            // Happens in the happy path when we already created the file and it is not a directory
        }
    }

    private void StartWatchingUserEventGridSettings(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        this._userEventGridSettingsFileWatcher = new FileSystemWatcher(Constants.RootDirectoryPath, Constants.EventGridSettingsFileName)
        {
            EnableRaisingEvents = true,
        };

        // FileSystemWatcher is known to fire multiple events for a single file change dependending on the text editor used.
        // Debounce the event to avoid unnecessary file reads and writes.
        // https://stackoverflow.com/a/1764809/1210053
        var debouncedSyncOnFileChanged = Debounce(() => this.SyncGeneratedEventGridSettingsAsync(dependency, cancellationToken));

        this._userEventGridSettingsFileWatcher.Changed += (_, _) => debouncedSyncOnFileChanged();
        this._userEventGridSettingsFileWatcher.Created += (_, _) => debouncedSyncOnFileChanged();
        this._userEventGridSettingsFileWatcher.Deleted += (_, _) => debouncedSyncOnFileChanged();
        this._userEventGridSettingsFileWatcher.Renamed += (_, _) => debouncedSyncOnFileChanged();
    }

    private static Action Debounce(Func<Task> func)
    {
        // https://stackoverflow.com/a/29491927/825695
        CancellationTokenSource? cancellationTokenSource = null;

        return () =>
        {
            var previousCancellationTokenSource = cancellationTokenSource;
            if (previousCancellationTokenSource != null)
            {
                previousCancellationTokenSource.Cancel();
                previousCancellationTokenSource.Dispose();
            }

            cancellationTokenSource = new CancellationTokenSource();

            var reasonableFileEventDebounceDelay = TimeSpan.FromMilliseconds(100);
            _ = Task.Delay(reasonableFileEventDebounceDelay, cancellationTokenSource.Token)
                .ContinueWith(async task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        try
                        {
                            await func();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }, TaskScheduler.Default);
        };
    }

    private async Task SyncGeneratedEventGridSettingsAsync(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        await this._generatedEventGridSettingsFileWriteLock.WaitAsync(cancellationToken);

        try
        {
            // Merges the user-managed Event Grid settings with the topics declared in leap.yaml and create or overwrite the generated Event Grid settings file.
            var userEventGridSettings = await this.ReadUserEventGridSettingsAsync(cancellationToken);

            var generatedTopics = dependency.Topics.DeepClone();
            generatedTopics.Merge(userEventGridSettings?.Topics);
            var generatedEventGridSettings = new EventGridSettings(generatedTopics);

            await this.WriteGeneratedEventGridSettingsAsync(generatedEventGridSettings, cancellationToken);
        }
        finally
        {
            this._generatedEventGridSettingsFileWriteLock.Release();
        }
    }

    private async Task<EventGridSettings?> ReadUserEventGridSettingsAsync(CancellationToken cancellationToken)
    {
        EventGridSettings? userEventGridSettings = null;

        try
        {
            await using var stream = File.OpenRead(Constants.UserEventGridSettingsFilePath);
            userEventGridSettings = await JsonSerializer.DeserializeAsync(stream, EventGridSettingsSourceGenerationContext.Default.EventGridSettings, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            // The user might have deleted the file, we will recreate it the next time Leap starts
        }
        catch (JsonException)
        {
            logger.LogWarning(
                "Failed to deserialize user-managed Event Grid configuration at {UserEventGridSettingsFilePath}, the file might be malformed.",
                Constants.UserEventGridSettingsFilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "An unexpected error occurred while reading user-managed Event Grid configuration at {EventGridSettingsFilePath}: {ExceptionMessage}",
                Constants.UserEventGridSettingsFilePath, ex.Message);
        }

        return userEventGridSettings;
    }

    private async Task WriteGeneratedEventGridSettingsAsync(EventGridSettings eventGridSettings, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(Constants.GeneratedEventGridSettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, eventGridSettings, EventGridSettingsSourceGenerationContext.Default.EventGridSettings, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while writing the generated Event Grid settings at {EventGridSettingsFilePath}", Constants.GeneratedEventGridSettingsFilePath);
        }
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = new DockerComposeImageName("workleap/eventgridemulator:0.6.7"),
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports =
            {
                new DockerComposePortMappingYaml(EventGridPort, EventGridPort)
            },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(Constants.GeneratedEventGridSettingsFilePath, "/app/appsettings.json", DockerComposeConstants.Volume.ReadOnly),
                new DockerComposeVolumeMappingYaml(Constants.CertificatesDirectoryPath, "/cert", DockerComposeConstants.Volume.ReadOnly),
            },
            Environment =
            {
                ["Kestrel__Certificates__Default__Path"] = $"/cert/{Constants.LeapCertificateCrtFileName}",
                ["Kestrel__Certificates__Default__KeyPath"] = $"/cert/{Constants.LeapCertificateKeyFileName}",
                ["ASPNETCORE_URLS"] = $"https://*:{EventGridPort}",
            }
        };

        dockerComposeYaml.Services[ServiceName] = service;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("Azure__EventGrid__Endpoint", EventGridHostUrl, EnvironmentVariableScope.Host),
            new EnvironmentVariable("Azure__EventGrid__Endpoint", EventGridContainerUrl, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["Azure:EventGrid:Endpoint"] = EventGridHostUrl;
    }

    public void Dispose()
    {
        this._userEventGridSettingsFileWatcher?.Dispose();
        this._generatedEventGridSettingsFileWriteLock.Dispose();
    }
}