using System.Text.Json.Nodes;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal sealed class FusionAuthDependencyHandler : DependencyHandler<FusionAuthDependency>
{
    public const int HostFusionAuthPort = 9011;
    private const int ContainerFusionAuthPort = 9011;

    private const string ServiceName = "fusionauth";
    private const string ContainerName = "leap-fusionauth";
    private const string VolumeName = "leap_fusionauth_data";

    private static readonly string HostConnectionString = $"http://127.0.0.1:{HostFusionAuthPort}/";
    private static readonly string ContainerConnectionString = $"http://host.docker.internal:{ContainerFusionAuthPort}:9011/";

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly ILogger _logger;

    public FusionAuthDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        ILogger<FusionAuthDependencyHandler> logger)
    {
        this._environmentVariables = environmentVariables;
        this._dockerCompose = dockerCompose;
        this._appSettingsJson = appSettingsJson;
        this._logger = logger;
    }

    private static async void WriteKickstartFile()
    {
        /*lang=json,strict*/
        var json = """
{
    "variables": {
        "adminEmail": "admin@workleap.com",
        "adminPassword": "localpassword",
        "defaultTenantId": "30663132-6464-6665-3032-326466613934"
    },
    "apiKeys": [
        {
            "key": "local-fusionauth-provisioning-api-key",
            "description": "API key used for local development FusionAuth provisioning only",
            "keyManager": true
        }
    ],
    "requests": [
        {
            "method": "POST",
            "url": "/api/user/registration/00000000-0000-0000-0000-000000000001",
            "body": {
                "user": {
                    "email": "#{adminEmail}",
                    "password": "#{adminPassword}",
                    "firstName": "Workleap",
                    "lastName": "Admin"
                },
                "registration": {
                    "applicationId": "#{FUSIONAUTH_APPLICATION_ID}",
                    "roles": [
                        "admin"
                    ]
                }
            }
        }
    ]
}
""";

        await File.WriteAllTextAsync(Constants.FusionAuthKickstartFilePath, json);
    }

    protected override Task BeforeStartAsync(FusionAuthDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackFusionAuthStart();
        WriteKickstartFile();
        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "fusionauth/fusionauth-app:1.49.2",
            ContainerName = ContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Environment = new KeyValueCollectionYaml
            {
                ["DATABASE_URL"] = "jdbc:postgresql://postgres:5432/fusionauth",
                ["DATABASE_ROOT_USERNAME"] = "postgres",
                ["DATABASE_ROOT_PASSWORD"] = "localpassword",
                ["DATABASE_USERNAME"] = "postgres",
                ["DATABASE_PASSWORD"] = "localpassword",
                ["FUSIONAUTH_APP_MEMORY"] = "1G",
                ["FUSIONAUTH_APP_RUNTIME_MODE"] = "development",
                ["FUSIONAUTH_APP_URL"] = "http://fusionauth:9011",
                ["SEARCH_TYPE"] = "database",
                ["FUSIONAUTH_APP_KICKSTART_FILE"] = "/usr/local/fusionauth/kickstart/" + Constants.FusionAuthKickstartFileName,
            },
            Ports = { new DockerComposePortMappingYaml(HostFusionAuthPort, ContainerFusionAuthPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml("leap_fusionauth_data", "/usr/local/fusionauth/config", DockerComposeConstants.Volume.ReadWrite),
                new DockerComposeVolumeMappingYaml(Constants.FusionAuthKickstartFilePath, "/usr/local/fusionauth/kickstart/" + Constants.FusionAuthKickstartFileName, DockerComposeConstants.Volume.ReadWrite),
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
            new EnvironmentVariable("CONNECTIONSTRINGS__FUSIONAUTH", HostConnectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("CONNECTIONSTRINGS__FUSIONAUTH", ContainerConnectionString, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["ConnectionStrings:FusionAuth"] = HostConnectionString;
    }

    protected override Task AfterStartAsync(FusionAuthDependency dependency, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("FusionAuth instance is ready");

        return Task.CompletedTask;
    }
}