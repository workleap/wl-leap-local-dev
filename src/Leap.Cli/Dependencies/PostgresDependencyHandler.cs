using System.Text.Json.Nodes;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyHandler : DependencyHandler<PostgresDependency>
{
    public const int HostPostgresPort = 5442;
    private const int ContainerPostgresPort = 5432;

    private const string ServiceName = "postgres";
    private const string ContainerName = "leap-postgres";
    private const string VolumeName = "leap_postgres_data";

    private static readonly string HostConnectionString = $"postgresql://127.0.0.1:{HostPostgresPort}/postgres?user=postgres&password=localpassword";
    private static readonly string ContainerConnectionString = $"postgresql://host.docker.internal:{ContainerPostgresPort}/postgres?user=postgres&password=localpassword";

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly ILogger _logger;

    public PostgresDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        ILogger<PostgresDependencyHandler> logger)
    {
        this._environmentVariables = environmentVariables;
        this._dockerCompose = dockerCompose;
        this._appSettingsJson = appSettingsJson;
        this._logger = logger;
    }

    protected override Task BeforeStartAsync(PostgresDependency dependency, CancellationToken cancellationToken)
    {
        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "postgres:16",
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Environment = new KeyValueCollectionYaml
            {
                ["POSTGRES_DB"] = "postgres",
                ["POSTGRES_USER"] = "postgres",
                ["POSTGRES_PASSWORD"] = "localpassword",
            },
            Ports = { new DockerComposePortMappingYaml(HostPostgresPort, ContainerPostgresPort) },
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
        };

        dockerComposeYaml.Services[ServiceName] = service;
        dockerComposeYaml.Volumes[VolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(new[]
        {
            new EnvironmentVariable("CONNECTIONSTRINGS__POSTGRESQL", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("CONNECTIONSTRINGS__POSTGRESQL", ContainerConnectionString, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:PostgreSQL"] = HostConnectionString;
    }

    protected override Task AfterStartAsync(PostgresDependency dependency, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("PostgreSQL instance is ready");

        return Task.CompletedTask;
    }
}