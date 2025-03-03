using System.Text.Json.Nodes;
using CliWrap;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Leap.Cli.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies;

internal sealed partial class MongoDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    ICliWrap cliWrap,
    IAspireManager aspire)
    : DependencyHandler<MongoDependency>
{
    public const int MongoPort = 27217;

    public const string ServiceName = MongoDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-mongo";
    private const string DataVolumeName = "leap_mongo_data";
    private const string ConfigVolumeName = "leapmongo_config";
    private const string ReplicaSetName = "rs0";

    private static readonly string NonReplicaSetConnectionString = $"mongodb://127.0.0.1:{MongoPort}";
    private static readonly string ReplicaSetConnectionString = $"{NonReplicaSetConnectionString}/?replicaSet={ReplicaSetName}";

    private const string ReplicaSetInitScriptFileName = "mongo-init-replica-set.js";
    private static readonly string ReplicaSetInitScriptHostFilePath = Path.Combine(Constants.GeneratedDirectoryPath, ReplicaSetInitScriptFileName);
    private const string ReplicaSetInitScriptContainerFilePath = $"/leap/{ReplicaSetInitScriptFileName}";

    protected override async Task HandleAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackMongodbStart();

        ConfigureDockerCompose(dependency, dockerCompose.Configuration);
        environmentVariables.Configure(envVars => ConfigureEnvironmentVariables(dependency, envVars));
        ConfigureAppSettingsJson(dependency, appSettingsJson.Configuration);

        await WriteReplicaSetInitScriptAsync(cancellationToken);

        aspire.Builder.AddDockerComposeResource(new DockerComposeResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [dependency.UseReplicaSet ? ReplicaSetConnectionString : NonReplicaSetConnectionString],
        });

        aspire.Builder.Eventing.Subscribe<ResourceReadyEvent>(ServiceName, async (evt, ct) =>
        {
            var resourceLogger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(ServiceName);
            await this.OnMongoResourceReady(resourceLogger, ct);
        });
    }

    private static void ConfigureDockerCompose(MongoDependency dependency, DockerComposeYaml dockerComposeYaml)
    {
        // enableDetailedConnectionHealthMetricLogLines=false removes a lot of noise from the logs
        // https://www.mongodb.com/docs/manual/reference/parameters/#mongodb-parameter-param.enableDetailedConnectionHealthMetricLogLines
        InlinedQuotedStringCollectionYaml command =
        [
            "--quiet", "--bind_ip_all", "--port", MongoPort.ToString(), "--replSet", ReplicaSetName, "--setParameter", "enableDetailedConnectionHealthMetricLogLines=false"
        ];

        var service = new DockerComposeServiceYaml
        {
            Image = new DockerComposeImageName("mongo:8.0.5"),
            ContainerName = ContainerName,
            Command = command,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(MongoPort, MongoPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(DataVolumeName, "/data/db", DockerComposeConstants.Volume.ReadWrite),
                new DockerComposeVolumeMappingYaml(ConfigVolumeName, "/data/configdb", DockerComposeConstants.Volume.ReadWrite),
                new DockerComposeVolumeMappingYaml(ReplicaSetInitScriptHostFilePath, ReplicaSetInitScriptContainerFilePath, DockerComposeConstants.Volume.ReadOnly),
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
                // https://stackoverflow.com/a/74709736/825695
                Test = ["CMD", "mongosh", "--port", MongoPort.ToString(), "--quiet", "--eval", "db.adminCommand('ping')"],
                Interval = "2s",
                Retries = 30,
            }
        };

        dockerComposeYaml.Services[ServiceName] = service;

        dockerComposeYaml.Volumes[DataVolumeName] = null;
        dockerComposeYaml.Volumes[ConfigVolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(MongoDependency dependency, List<EnvironmentVariable> environmentVariables)
    {
        var connectionString = dependency.UseReplicaSet ? ReplicaSetConnectionString : NonReplicaSetConnectionString;

        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(
        [
            new EnvironmentVariable("ConnectionStrings__Mongo", connectionString, EnvironmentVariableScope.Host),
            new EnvironmentVariable("ConnectionStrings__Mongo", HostNameResolver.ReplaceLocalhostWithContainerHost(connectionString), EnvironmentVariableScope.Container)
        ]);
    }

    private static void ConfigureAppSettingsJson(MongoDependency dependency, JsonObject appsettings)
    {
        appsettings["ConnectionStrings:Mongo"] = dependency.UseReplicaSet ? ReplicaSetConnectionString : NonReplicaSetConnectionString;
    }

    private async Task OnMongoResourceReady(ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("=================================================================");
        logger.LogInformation("Initializing MongoDB replica set '{ReplicaSet}'...", ReplicaSetName);

        try
        {
            var command = new Command("docker")
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .WithWorkingDirectory(Constants.DockerComposeDirectoryPath)
                .WithArguments(["compose", "--file", Constants.DockerComposeFilePath, "exec", ServiceName, "mongosh", "--port", MongoPort.ToString(), ReplicaSetInitScriptContainerFilePath])
                .WithStandardOutputPipe(PipeTarget.ToDelegate(x => logger.LogInformation("{Output}", x)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(x => logger.LogError("{Output}", x)));

            await cliWrap.ExecuteBufferedAsync(command, cancellationToken);

            logger.LogInformation("MongoDB replica set is ready");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start MongoDB replica set");
        }

        logger.LogInformation("=================================================================");
    }
}