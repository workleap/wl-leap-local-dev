using System.Text.Json.Nodes;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal class FusionAuthDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariable,
    IConfigureAppSettingsJson appSettingsJson,
    IDockerComposeManager dockerComposeManager,
    ILogger<FusionAuthDependencyHandler> logger,
    IAspireManager aspireManager) : DependencyHandler<FusionAuthDependency>
{
    public const int FusionAuthPort = 9020;

    private const string AppServiceName = "fusionauth";
    private const string DbServiceName = "fusionauthdb";
    private const string ProxyServiceName = "fusionauthproxy";

    private const string AppContainerName = "leap-fa-app";
    private const string DbContainerName = "leap-fa-db";
    private const string ProxyContainerName = "leap-fa-proxy";
    private const string ProvisioningContainerName = "leap-fa-provisioning";

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

        var fusionAuthResource = new DockerComposeResource(AppServiceName, AppContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [$"https://{HostConnectionString}"]
        };
        this.AddResetFusionAuthCommand(fusionAuthResource);

        aspireManager.Builder.AddDockerComposeResource(fusionAuthResource);
        aspireManager.Builder.AddDockerComposeResource(
            new DockerComposeResource(Constants.FusionAuthProvisioningServiceName, ProvisioningContainerName)
            {
                InitialState = KnownResourceStates.Finished
            })
        .WaitFor([AppServiceName]);

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

        var fusionAuthProvisioning = new DockerComposeServiceYaml()
        {
            Image = new DockerComposeImageName(Constants.FusionAuthProvisioningImage),
            ContainerName = ProvisioningContainerName,
            DependsOn = [AppServiceName],
            Restart = "no",
            Environment = new KeyValueCollectionYaml()
            {
                ["DOTNET_ENVIRONMENT"] = "Local",
                ["Services__FusionAuth__BaseURL"] = $"http://{AppContainerName}:9011"
            },
            SecurityOption = ["no-new-privileges:true"]
        };

        dockerComposeYaml.Services[AppServiceName] = fusionAuthApp;
        dockerComposeYaml.Services[DbServiceName] = fusionAuthDb;
        dockerComposeYaml.Services[ProxyServiceName] = fusionAuthProxy;
        dockerComposeYaml.Services[Constants.FusionAuthProvisioningServiceName] = fusionAuthProvisioning;

        dockerComposeYaml.Volumes[ConfigVolumeName] = null;
        dockerComposeYaml.Volumes[DbVolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("Services__FusionAuth__BaseURL", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("Services__FusionAuth__BaseURL", ContainerConnectionString, EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:FusionAuth"] = HostConnectionString;
    }

    private void AddResetFusionAuthCommand(DockerComposeResource resource)
    {
        var command = new ResourceCommandAnnotation(
            name: "reset-fusionauth",
            displayName: "Reset FusionAuth",
            updateState: context => KnownResourceStates.TerminalStates.Contains(context.ResourceSnapshot.State?.Text) ? ResourceCommandState.Disabled : ResourceCommandState.Enabled,
            executeCommand: async context =>
            {
                try
                {
                    TelemetryMeters.TrackFusionAuthResets();
                    await dockerComposeManager.ClearDockerComposeServiceVolumeAsync(AppServiceName, logger, context.CancellationToken);
                    await dockerComposeManager.ClearDockerComposeServiceVolumeAsync(ProxyServiceName, logger, context.CancellationToken);
                    await dockerComposeManager.ClearDockerComposeServiceVolumeAsync(DbServiceName, logger, context.CancellationToken);

                    await dockerComposeManager.StartDockerComposeServiceAsync(DbServiceName, logger, context.CancellationToken);
                    await dockerComposeManager.StartDockerComposeServiceAsync(ProxyServiceName, logger, context.CancellationToken);
                    await dockerComposeManager.StartDockerComposeServiceAsync(AppServiceName, logger, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    return new ExecuteCommandResult
                    {
                        ErrorMessage = "An error occurred while trying to reset FusionAuth: " + ex.Message,
                        Success = false
                    };
                }

                return CommandResults.Success();
            },
            displayDescription: null,
            parameter: null,
            confirmationMessage: "Are you sure you want to reset FusionAuth configurations? You may lose any configurations added afterwards. Select 'Yes' to proceed.",
            iconName: "ArrowRotateClockwise", // FluentUI icons found here: https://bennymeg.github.io/ngx-fluent-ui/
            iconVariant: IconVariant.Filled,
            isHighlighted: false);

        resource.Annotations.Add(command);
    }
}