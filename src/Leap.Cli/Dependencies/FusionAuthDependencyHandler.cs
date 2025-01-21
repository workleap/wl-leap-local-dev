using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;

namespace Leap.Cli.Dependencies;

internal class FusionAuthDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariable,
    IConfigureAppSettingsJson appSettingsJson,
    IAspireManager aspireManager) : DependencyHandler<FusionAuthDependency>
{
    public const int FusionAuthPort = 9020;

    private const string AppServiceName = "fusionauth";
    private const string DbServiceName = "fusionauthdb";
    private const string ProxyServiceName = "fusionauthproxy";

    private const string AppContainerName = "leap-fa-app";
    private const string DbContainerName = "leap-fa-db";
    private const string ProxyContainerName = "leap-fa-proxy";

    private const string ConfigVolumeName = "leap_fa_app_config";
    private const string DbVolumeName = "leap_fa_db_data";

    private const string KickstartConfigFilePath = "/usr/local/fusionauth/kickstart.json";

    private static readonly string HostNginxConfigFilePath = Path.Combine(Constants.FusionAuthDirectoryPath, "nginx-ssl-reverse-proxy.conf");
    private static readonly string HostKickstartConfigFilePath = Path.Combine(Constants.FusionAuthDirectoryPath, "kickstart.json");

    private static readonly string HostConnectionString = $"127.0.0.1:{FusionAuthPort}";
    private static readonly string ContainerConnectionString = $"host.docker.internal:{FusionAuthPort}";

    protected override Task HandleAsync(FusionAuthDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackFusionAuthStart();
        ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariable.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        aspireManager.Builder.AddDockerComposeResource(new DockerComposeResource(AppServiceName, AppContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [$"https://{HostConnectionString}"]
        });

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var fusionAuthApp = new DockerComposeServiceYaml()
        {
            Image = new DockerComposeImageName("fusionauth/fusionauth-app:1.54.0"),
            ContainerName = AppContainerName,
            DependsOn = [DbServiceName, ProxyServiceName],
            Environment = new KeyValueCollectionYaml()
            {
                { "DATABASE_URL", $"jdbc:postgresql://{DbContainerName}:5432/fusionauth" },
                { "DATABASE_ROOT_USERNAME", "postgres" },
                { "DATABASE_ROOT_PASSWORD", "postgres" },
                { "DATABASE_USERNAME", "fusionauth" },
                { "DATABASE_PASSWORD", "local_dev_only_nothing_secret_here" },
                { "FUSIONAUTH_APP_MEMORY", "256M" },
                { "FUSIONAUTH_APP_RUNTIME_MODE", "development" },
                { "FUSIONAUTH_APP_URL", $"http://{AppContainerName}:9011" },
                { "SEARCH_TYPE", "database" },
                { "FUSIONAUTH_APP_KICKSTART_FILE", KickstartConfigFilePath },
            },
            Restart = "unless-stopped",
            Volumes =
            [
                new DockerComposeVolumeMappingYaml(ConfigVolumeName, "/usr/local/fusionauth/config"),
                new DockerComposeVolumeMappingYaml(HostKickstartConfigFilePath, KickstartConfigFilePath)
            ],
            SecurityOption = ["no-new-privileges:true"]
        };

        var fusionAuthDb = new DockerComposeServiceYaml()
        {
            Image = new DockerComposeImageName("postgres:16-alpine"),
            ContainerName = DbContainerName,
            Environment = new KeyValueCollectionYaml()
            {
                ["POSTGRES_USER"] = "postgres",
                ["POSTGRES_PASSWORD"] = "postgres",
                ["PGDATA"] = "/var/lib/postgresql/data/pgdata"
            },
            Restart = "unless-stopped",
            Volumes =
            [
                new DockerComposeVolumeMappingYaml(DbVolumeName, "/var/lib/postgresql/data")
            ],
            SecurityOption = ["no-new-privileges:true"]
        };

        var fusionAuthProxy = new DockerComposeServiceYaml()
        {
            Image = new DockerComposeImageName("nginx:1.27-alpine"),
            ContainerName = ProxyContainerName,
            DependsOn = [DbServiceName],
            Restart = "unless-stopped",
            Ports = [new DockerComposePortMappingYaml(9020, 9020)],
            Volumes =
            [
                new DockerComposeVolumeMappingYaml(HostNginxConfigFilePath, "/etc/nginx/conf.d/fusionauth.conf"),
                new DockerComposeVolumeMappingYaml(Constants.LocalCertificateCrtFilePath, "/etc/ssl/certs/localhost.crt"),
                new DockerComposeVolumeMappingYaml(Constants.LocalCertificateKeyFilePath, "/etc/ssl/certs/localhost.key")
            ],
            SecurityOption = ["no-new-privileges:true"]
        };

        dockerComposeYaml.Services[AppServiceName] = fusionAuthApp;
        dockerComposeYaml.Services[DbServiceName] = fusionAuthDb;
        dockerComposeYaml.Services[ProxyServiceName] = fusionAuthProxy;

        dockerComposeYaml.Volumes[ConfigVolumeName] = null;
        dockerComposeYaml.Volumes[DbVolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("ConnectionStrings__FusionAuth", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("ConnectionStrings__FusionAuth", ContainerConnectionString, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:FusionAuth"] = HostConnectionString;
    }
}