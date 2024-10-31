using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class SqlServerDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    IPlatformHelper platformHelper,
    IAspireManager aspire)
    : DependencyHandler<SqlServerDependency>
{
    public const int HostSqlServerPort = 1444;
    private const int ContainerSqlServerPort = 1433;

    private const string ServiceName = SqlServerDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-sqlserver";
    private const string VolumeName = "leap_sqlserver_data";

    private static readonly string HostConnectionString = $"Server=127.0.0.1,{HostSqlServerPort}; Database=master; User=sa; Password=L0c@lh0st!; Encrypt=True; TrustServerCertificate=True";
    private static readonly string ContainerConnectionString = $"Server=host.docker.internal,{ContainerSqlServerPort}; Database=master; User=sa; Password=L0c@lh0st!; Encrypt=True; TrustServerCertificate=True";

    protected override Task HandleAsync(SqlServerDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackSqlServerStart();
        this.ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        aspire.Builder.AddExternalContainer(new ExternalContainerResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
        });

        return Task.CompletedTask;
    }

    private void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        // TODO Users with M1 chips have complained that mcr.microsoft.com/mssql/server doesn't work on their machines. Validate this.
        var image = platformHelper.ProcessArchitecture == Architecture.Arm64 && platformHelper.CurrentOS == OSPlatform.OSX
            ? new DockerComposeImageName("mcr.microsoft.com/azure-sql-edge:2.0.0")
            : new DockerComposeImageName("mcr.microsoft.com/mssql/server:2022-latest");

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
            Ports =
            {
                new DockerComposePortMappingYaml(HostSqlServerPort, ContainerSqlServerPort)
            },
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
}