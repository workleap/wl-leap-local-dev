using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Dependencies;

internal sealed class EventGridDependencyHandler : DependencyHandler<EventGridDependency>
{
    private const int EventGridPort = 6500;
    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<EventGridDependencyHandler> _logger;

    private const string ServiceName = "eventgrid";
    private const string ContainerName = "leap-eventgrid";

    private static readonly string EventGridHostUrl = $"http://127.0.0.1:{EventGridPort}";
    private static readonly string EventGridContainerUrl = $"host.docker.internal:{EventGridPort}";

    public EventGridDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        IFileSystem fileSystem,
        ILogger<EventGridDependencyHandler> logger)
    {
        this._dockerCompose = dockerCompose;
        this._environmentVariables = environmentVariables;
        this._appSettingsJson = appSettingsJson;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    protected override async Task BeforeStartAsync(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackEventGridStart();
        await this.EnsureEventGridSettingsFileExists(cancellationToken);
        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);
    }

    protected override Task AfterStartAsync(EventGridDependency dependency, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Event Grid emulator is up and running, you can edit your topic registrations at {FilePath}", Constants.LeapEventGridSettingsFilePath);
        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "workleap/eventgridemulator:0.4.0",
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(EventGridPort, EventGridPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(Constants.LeapEventGridSettingsFilePath, "/app/appsettings.json",  DockerComposeConstants.Volume.ReadOnly)
            },
        };

        dockerComposeYaml.Services[ServiceName] = service;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        environmentVariables.AddRange(new[]
        {
            new EnvironmentVariable("AZURE__EVENTGRID__ENDPOINT", EventGridHostUrl, EnvironmentVariableScope.Host),
            new EnvironmentVariable("AZURE__EVENTGRID__ENDPOINT", EventGridContainerUrl, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["Azure:EventGrid:Endpoint"] = EventGridHostUrl;
    }

    private async Task EnsureEventGridSettingsFileExists(CancellationToken cancellationToken)
    {
        if (!this._fileSystem.File.Exists(Constants.LeapEventGridSettingsFilePath))
        {
            await using var stream = this._fileSystem.File.OpenWrite(Constants.LeapEventGridSettingsFilePath);
            await JsonSerializer.SerializeAsync(stream, new EventGridSettings(), EventGridSettingsSourceGenerationContext.Default.EventGridSettings, cancellationToken);
        }
    }
}
