using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    IAspireManager aspire)
    : DependencyHandler<PostgresDependency>
{
    public const int HostPostgresPort = 5442;
    private const int ContainerPostgresPort = 5432;

    private const string ServiceName = PostgresDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-postgres";
    private const string VolumeName = "leap_postgres_data";

    private const string McpResourceName = "postgres-mcp";
    private const int ContainerMcpPort = 8000;

    private static readonly string McpDatabaseUri = $"postgresql://postgres:localpassword@host.docker.internal:{HostPostgresPort}/postgres";

    private static readonly string HostConnectionString = $"Host=localhost;Port={HostPostgresPort};Database=postgres;Username=postgres;Password=localpassword";
    private static readonly string ContainerConnectionString = $"Host=postgres;Port={ContainerPostgresPort};Database=postgres;Username=postgres;Password=localpassword";

    protected override Task HandleAsync(PostgresDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackPostgresStart();
        ConfigureDockerCompose(dockerCompose.Configuration, dependency.ImageTag);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        var postgresResource = aspire.Builder.AddDockerComposeResource(new DockerComposeResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [HostConnectionString]
        });

        if (dependency.Mcp)
        {
            // Use Aspire's native container API so WithMcpServer() registers it with the dashboard MCP proxy.
            // https://github.com/dotnet/aspire/blob/v13.2.1/src/Aspire.Hosting.PostgreSQL/PostgresBuilderExtensions.cs
#pragma warning disable ASPIREMCP001 // WithMcpServer is experimental
            aspire.Builder.AddContainer(McpResourceName, "crystaldba/postgres-mcp", "0.3.0")
                .WithHttpEndpoint(targetPort: ContainerMcpPort)
                .WithArgs("--access-mode=unrestricted", "--transport=sse")
                .WithEnvironment("DATABASE_URI", McpDatabaseUri)
                .WithMcpServer("/sse")
                .WaitFor(postgresResource);
#pragma warning restore ASPIREMCP001
        }

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml, string? imageTag)
    {
        const string dbName = "postgres";
        const string pgUser = "postgres";
        const string pgPassword = "localpassword";

        var service = new DockerComposeServiceYaml
        {
            Image = imageTag != null ? new DockerComposeImageName($"postgres:{imageTag}") : new DockerComposeImageName("postgres:18.3-alpine"),
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Environment = new KeyValueCollectionYaml
            {
                ["POSTGRES_DB"] = dbName,
                ["POSTGRES_USER"] = pgUser,
                ["POSTGRES_PASSWORD"] = pgPassword,
            },
            Ports =
            {
                new DockerComposePortMappingYaml(HostPostgresPort, ContainerPostgresPort)
            },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(VolumeName, "/var/lib/postgresql/data", DockerComposeConstants.Volume.ReadWrite),
            },
            Deploy = new DockerComposeDeploymentYaml
            {
                Resources = new DockerComposeResourcesYaml
                {
                    Limits = new DockerComposeCpusAndMemoryYaml
                    {
                        Cpus = "0.5",
                        Memory = "1G",
                    },
                },
            },
            Healthcheck = new DockerComposeHealthcheckYaml
            {
                // Inspired by https://github.com/search?q=path%3Adocker-compose.yml+pg_isready&type=code
                Test = ["CMD", "pg_isready", "-U", pgUser, "-d", dbName, "-p", ContainerPostgresPort.ToString()],
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
            new EnvironmentVariable("ConnectionStrings__PostgreSQL", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("ConnectionStrings__PostgreSQL", ContainerConnectionString, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:PostgreSQL"] = HostConnectionString;
    }
}