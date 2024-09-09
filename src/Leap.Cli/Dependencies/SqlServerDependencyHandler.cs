using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal sealed class SqlServerDependencyHandler : DependencyHandler<SqlServerDependency>
{
    public const int HostSqlServerPort = 1444;
    private const int ContainerSqlServerPort = 1433;

    private const string ServiceName = "sqlserver";
    private const string ContainerName = "leap-sqlserver";
    private const string VolumeName = "leap_sqlserver_data";

    private static readonly string HostConnectionString = $"Server=127.0.0.1,{HostSqlServerPort}; Database=master; User=sa; Password=L0c@lh0st!; Encrypt=True; TrustServerCertificate=True";
    private static readonly string ContainerConnectionString = $"Server=host.docker.internal,{ContainerSqlServerPort}; Database=master; User=sa; Password=L0c@lh0st!; Encrypt=True; TrustServerCertificate=True";

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger _logger;

    public SqlServerDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        IPlatformHelper platformHelper,
        ILogger<SqlServerDependencyHandler> logger)
    {
        this._environmentVariables = environmentVariables;
        this._dockerCompose = dockerCompose;
        this._appSettingsJson = appSettingsJson;
        this._platformHelper = platformHelper;
        this._logger = logger;
    }

    protected override Task BeforeStartAsync(SqlServerDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackSqlServerStart();
        this.ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        return Task.CompletedTask;
    }

    private void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        // TODO Users with M1 chips have complained that mcr.microsoft.com/mssql/server doesn't work on their machines. Validate this.
        var image = this._platformHelper.ProcessArchitecture == Architecture.Arm64 && this._platformHelper.CurrentOS == OSPlatform.OSX
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

        var service = new DockerComposeServiceYaml
        {
            Image = image,
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Environment = new KeyValueCollectionYaml
            {
                ["ACCEPT_EULA"] = "Y",
                ["MSSQL_SA_PASSWORD"] = "L0c@lh0st!",
                ["MSSQL_PID"] = "Developer", // This edition is free for development purposes
            },
            Ports = { new DockerComposePortMappingYaml(HostSqlServerPort, ContainerSqlServerPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(VolumeName, "/var/opt/mssql", DockerComposeConstants.Volume.ReadWrite),
            },
            Deploy = new DockerComposeDeploymentYaml
            {
                Resources = new DockerComposeResourcesYaml
                {
                    Limits = new DockerComposeCpusAndMemoryYaml
                    {
                        // TODO Restrict the memory seems to break the container (tried 500M and 1G), investigate more?
                        Cpus = "0.5",
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
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("ConnectionStrings__SqlServer", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("ConnectionStrings__SqlServer", ContainerConnectionString, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:SqlServer"] = HostConnectionString;
    }

    protected override Task AfterStartAsync(SqlServerDependency dependency, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("SQL Server instance is ready");

        return Task.CompletedTask;
    }
}