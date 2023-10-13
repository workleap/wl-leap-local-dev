using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyHandler : DependencyHandler<MongoDependency>
{
    private const string ServiceName = "mongo";
    private const string VolumeName = "mongo_data";
    private const string ReplicaSetName = "rs0";

    private const int MongoPort = 27217;

    private readonly IConfigureDockerCompose _dockerCompose;

    public MongoDependencyHandler(IConfigureDockerCompose dockerCompose)
    {
        this._dockerCompose = dockerCompose;
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
            Command = $"--replSet {ReplicaSetName} --bind_ip_all --port {MongoPort}",
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

    protected override Task AfterStartAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}