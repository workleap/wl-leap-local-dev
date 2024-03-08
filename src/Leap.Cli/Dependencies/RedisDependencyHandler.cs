using System.Text.Json.Nodes;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyHandler : DependencyHandler<RedisDependency>
{
    public const int RedisPort = 6380;

    private const string ServiceName = "redis";
    private const string ContainerName = "leap-redis";
    private const string VolumeName = "leap_redis_data";

    private static readonly string HostConnectionString = $"127.0.0.1:{RedisPort}";
    private static readonly string ContainerConnectionString = $"host.docker.internal:{RedisPort}";

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly ILogger _logger;

    public RedisDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        ILogger<RedisDependencyHandler> logger)
    {
        this._dockerCompose = dockerCompose;
        this._environmentVariables = environmentVariables;
        this._appSettingsJson = appSettingsJson;
        this._logger = logger;
    }

    protected override Task BeforeStartAsync(RedisDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackRedisStart();
        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "redis:7-alpine",
            ContainerName = ContainerName,
            Command = new DockerComposeCommandYaml
            {
                // https://redis.io/docs/management/persistence/
                "--appendonly", "yes", // Use AOF (Append Only File) for incremental persistence
                "--save", "60", "1", // Write increment to disk if at least 1 key changed in the last 60 seconds
                "--port", RedisPort.ToString(),
            },
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(RedisPort, RedisPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(VolumeName, "/data", DockerComposeConstants.Volume.ReadWrite),
            },
            Deploy = new DockerComposeDeploymentYaml
            {
                Resources = new DockerComposeResourcesYaml
                {
                    Limits = new DockerComposeCpusAndMemoryYaml
                    {
                        Cpus = "0.5",
                        Memory = "500M",
                    },
                },
            },
        };

        dockerComposeYaml.Services[ServiceName] = service;
        dockerComposeYaml.Volumes[VolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(new[]
        {
            new EnvironmentVariable("CONNECTIONSTRINGS__REDIS", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("CONNECTIONSTRINGS__REDIS", ContainerConnectionString, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:Redis"] = HostConnectionString;
    }

    protected override Task AfterStartAsync(RedisDependency dependency, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Redis instance is ready");

        return Task.CompletedTask;
    }
}