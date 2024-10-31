using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

internal sealed class MongoDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    ICliWrap cliWrap,
    IAspireManager aspire)
    : DependencyHandler<MongoDependency>
{
    public const int MongoPort = 27217;

    private const string ServiceName = MongoDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-mongo";
    private const string DataVolumeName = "leap_mongo_data";
    private const string ConfigVolumeName = "leapmongo_config";
    private const string ReplicaSetName = "rs0";

    private static readonly string NonReplicaSetConnectionString = $"mongodb://127.0.0.1:{MongoPort}";
    private static readonly string ReplicaSetConnectionString = $"{NonReplicaSetConnectionString}/?replicaSet={ReplicaSetName}";

    protected override Task HandleAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackMongodbStart();
        ConfigureDockerCompose(dependency, dockerCompose.Configuration);
        environmentVariables.Configure(envVars => ConfigureEnvironmentVariables(dependency, envVars));
        ConfigureAppSettingsJson(dependency, appSettingsJson.Configuration);

        aspire.Builder.AddExternalContainer(new ExternalContainerResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [dependency.UseReplicaSet ? ReplicaSetConnectionString : NonReplicaSetConnectionString],
        });

        aspire.Builder.Eventing.Subscribe<ResourceReadyEvent>(ServiceName, async (evt, ct) =>
        {
            var resourceLogger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(ServiceName);
            await this.OnMongoResourceReady(dependency, resourceLogger, ct);
        });

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(MongoDependency dependency, DockerComposeYaml dockerComposeYaml)
    {
        InlinedQuotedStringCollectionYaml command = dependency.UseReplicaSet
            ? ["--quiet", "--bind_ip_all", "--port", MongoPort.ToString(), "--replSet", ReplicaSetName]
            : ["--quiet", "--bind_ip_all", "--port", MongoPort.ToString()];

        var service = new DockerComposeServiceYaml
        {
            Image = "mongo:7.0",
            ContainerName = ContainerName,
            Command = command,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(MongoPort, MongoPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(DataVolumeName, "/data/db", DockerComposeConstants.Volume.ReadWrite),
                new DockerComposeVolumeMappingYaml(ConfigVolumeName, "/data/configdb", DockerComposeConstants.Volume.ReadWrite),
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

    private async Task OnMongoResourceReady(MongoDependency dependency, ILogger logger, CancellationToken cancellationToken)
    {
        if (dependency.UseReplicaSet)
        {
            // TODO even if the replica set is already initialized, this can be a little slow (like, 2-5 seconds)
            // TODO we could have a cache layer in leap for arbitrary data, and store the container ID of the mongo container
            // TODO then we could check if the container ID is the same as the one in the cache, and if so, we can skip this step
            logger.LogInformation("Starting MongoDB replica set '{ReplicaSet}'...", ReplicaSetName);

            await this.EnsureReplicaSetReadyAsync(cancellationToken);

            // TODO that might not be true, improve the error handling
            logger.LogInformation("MongoDB replica set is ready");
        }
    }

    private async Task EnsureReplicaSetReadyAsync(CancellationToken cancellationToken)
    {
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts1.Token);

        while (!cts2.IsCancellationRequested)
        {
            var status = await this.GetReplicaSetStatusAsync(cts2.Token);

            if (status?.Ok == 1)
            {
                if (status.IsReady())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cts2.Token);
            }

            if (status?.IsNotYetInitialized() == true)
            {
                await this.InitiateReplicaSetAsync(cts2.Token);
            }
        }

        // TODO throw an exception or display an error, we couldn't start the replica set
    }

    // https://www.mongodb.com/docs/manual/reference/command/replSetGetStatus/#std-label-rs-status-output
    private async Task<ReplicaSetStatus?> GetReplicaSetStatusAsync(CancellationToken cancellationToken)
    {
        var statusOutput = await this.ExecuteMongoCommandAsync("rs.status()", cancellationToken);
        try
        {
            var status = JsonSerializer.Deserialize<ReplicaSetStatus>(statusOutput);

            if (status is null)
            {
                // TODO better error handling, we might want to retry. Maybe use Polly?
                throw new Exception($"Unable to retrieve replica set status. Output: {statusOutput}");
            }

            return status;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task InitiateReplicaSetAsync(CancellationToken cancellationToken)
    {
        var initCommand = "rs.initiate({_id:'" + ReplicaSetName + "',members:[{_id:0,host:'host.docker.internal:" + MongoPort + "'}]})";
        var initOutput = await this.ExecuteMongoCommandAsync(initCommand, cancellationToken);
        var initStatus = JsonSerializer.Deserialize<ReplicaSetStatus>(initOutput);
        if (initStatus?.Ok != 1)
        {
            throw new Exception($"Failed to initiate replica set. Output: {initOutput}");
        }
    }

    private async Task<string> ExecuteMongoCommandAsync(string mongoshCommand, CancellationToken cancellationToken)
    {
        var command = new Command("docker")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(Constants.DockerComposeDirectoryPath)
            .WithArguments(["compose", "exec", ServiceName, "mongosh", "--port", MongoPort.ToString(), "--quiet", "--eval", mongoshCommand, "--json", "relaxed"]);

        // TODO handle errors and unexpected behavior
        var result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);

        return result.StandardOutput;
    }

    private sealed class ReplicaSetStatus
    {
        [JsonPropertyName("ok")]
        public int? Ok { get; init; }

        [JsonPropertyName("codeName")]
        public string? CodeName { get; init; }

        [JsonPropertyName("myState")]
        public int? MyState { get; init; }

        public bool IsNotYetInitialized() => this.CodeName == "NotYetInitialized";

        public bool IsReady() => this.MyState == ReplicaSetState.Primary;
    }

    // https://www.mongodb.com/docs/manual/reference/replica-states/#replica-set-member-states
    private static class ReplicaSetState
    {
        public const int Primary = 1;
    }
}