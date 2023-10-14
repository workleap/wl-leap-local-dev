using System.Globalization;
using CliWrap;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Spectre.Console;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyHandler : DependencyHandler<MongoDependency>
{
    private const string ServiceName = "mongo";
    private const string VolumeName = "mongo_data";
    private const string ReplicaSetName = "rs0";

    private const int MongoPort = 27217;

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IAnsiConsole _console;

    public MongoDependencyHandler(IConfigureDockerCompose dockerCompose, IAnsiConsole console)
    {
        this._dockerCompose = dockerCompose;
        this._console = console;
    }

    protected override Task BeforeStartAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        this._dockerCompose.Configure(ConfigureMongo);
        return Task.CompletedTask;
    }

    private static void ConfigureMongo(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "mongo:7.0",
            Command = new DockerComposeCommandYaml { "--replSet", ReplicaSetName, "--bind_ip_all", "--port", MongoPort.ToString(CultureInfo.InvariantCulture) },
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(MongoPort, MongoPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(VolumeName, "/data/db", DockerComposeConstants.Volume.ReadWrite),
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
        };

        dockerComposeYaml.Services[ServiceName] = service;
        dockerComposeYaml.Volumes[VolumeName] = null;
    }

    protected override async Task AfterStartAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        await this._console.Status().StartAsync("Starting MongoDB replica set...", async _ =>
        {
            var exitCode = 0;
            var nbRetry = 5;

            do
            {
                try
                {
                    var replicateScript = "\"do { try { rs.status().ok; break; } catch (err) { rs.initiate({_id:'" + ReplicaSetName + "',members:[{_id:0,host:'host.docker.internal:" + MongoPort + "'}]}).ok } } while (true)\"";
                    var mongoPort = MongoPort.ToString(CultureInfo.InvariantCulture);

                    // TODO consider parsing mongosh results from JSON using the "--json relaxed" argument
                    var result = await new Command("docker")
                        .WithValidation(CommandResultValidation.None)
                        .WithWorkingDirectory(ConfigurationConstants.GeneratedDirectoryPath)
                        .WithArguments(new[] { "compose", "exec", "mongo", "mongosh", "--port", mongoPort, "--quiet", "--eval", replicateScript })
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                        .ExecuteAsync(cancellationToken);

                    exitCode = result.ExitCode;
                }
                catch (Exception ex)
                {
                    this._console.WriteException(ex);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    exitCode = 1;
                }
            }
            while (exitCode != 0 && --nbRetry > 0);

            if (exitCode == 0)
            {
                var connectionString = $"mongodb://127.0.0.1:{MongoPort}/?replicaSet={ReplicaSetName}";
                this._console.MarkupLineInterpolated($"MongoDB replica ready, the connection string is [green]{connectionString}[/]");
            }
        });
    }
}