using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    IAspireManager aspire)
    : DependencyHandler<RedisDependency>
{
    public const int RedisPort = 6380;

    private const string ServiceName = RedisDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-redis";
    private const string VolumeName = "leap_redis_data";

    private static readonly string HostConnectionString = $"127.0.0.1:{RedisPort}";
    private static readonly string ContainerConnectionString = $"host.docker.internal:{RedisPort}";

    protected override Task HandleAsync(RedisDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackRedisStart();
        ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        aspire.Builder.AddExternalContainer(new ExternalContainerResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
        });

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = new DockerComposeImageName("redis:7.4.1-alpine"),
            ContainerName = ContainerName,
            Command =
            [
                // https://redis.io/docs/management/persistence/
                "--appendonly", "yes", // Use AOF (Append Only File) for incremental persistence
                "--save", "60", "1", // Write increment to disk if at least 1 key changed in the last 60 seconds
                "--port", RedisPort.ToString(),
            ],
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports =
            {
                new DockerComposePortMappingYaml(RedisPort, RedisPort)
            },
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
            Healthcheck = new DockerComposeHealthcheckYaml
            {
                // https://stackoverflow.com/a/71504657/825695
                Test = ["CMD-SHELL", $"redis-cli -p {RedisPort} ping | grep PONG"],
                Interval = "1s",
                Retries = 30,
            }
        };

        dockerComposeYaml.Services[ServiceName] = service;
        dockerComposeYaml.Volumes[VolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("ConnectionStrings__Redis", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("ConnectionStrings__Redis", ContainerConnectionString, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:Redis"] = HostConnectionString;
    }
}