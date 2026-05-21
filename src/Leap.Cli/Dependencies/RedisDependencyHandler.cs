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

    private const string McpResourceName = "redis-mcp";
    private const int ContainerMcpPort = 8000;

    private static readonly string HostConnectionString = $"127.0.0.1:{RedisPort}";
    private static readonly string ContainerConnectionString = $"host.docker.internal:{RedisPort}";

    protected override Task HandleAsync(RedisDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackRedisStart();
        ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        var redisResource = aspire.Builder.AddDockerComposeResource(new DockerComposeResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [$"tcp://{HostConnectionString}"],
        });

        if (dependency.Mcp)
        {
            // The mcp/redis image defaults to stdio transport. Override the entrypoint
            // to start the FastMCP server in SSE mode so Aspire's MCP proxy can connect.
            // We must set host to 0.0.0.0 directly since the FASTMCP_HOST env var is not
            // picked up after the FastMCP instance is created.
#pragma warning disable ASPIREMCP001 // WithMcpServer is experimental
            aspire.Builder.AddContainer(McpResourceName, "mcp/redis", "latest")
                .WithHttpEndpoint(targetPort: ContainerMcpPort)
                .WithEntrypoint("uv")
                .WithArgs("run", "python", "-c",
                    "from src.common.server import mcp; mcp.settings.host = '0.0.0.0'; mcp.run(transport='sse')")
                .WithEnvironment("REDIS_HOST", "host.docker.internal")
                .WithEnvironment("REDIS_PORT", RedisPort.ToString())
                .WithMcpServer("/sse")
                .WaitFor(redisResource);
#pragma warning restore ASPIREMCP001
        }

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = new DockerComposeImageName("redis:8.6.3-alpine"),
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