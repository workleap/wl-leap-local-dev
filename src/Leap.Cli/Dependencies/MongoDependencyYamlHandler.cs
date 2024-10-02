using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyYamlHandler : IDependencyYamlHandler<MongoDependencyYaml>
{
    public MongoDependencyYaml Merge(MongoDependencyYaml leftYaml, MongoDependencyYaml rightYaml)
    {
        return new MongoDependencyYaml
        {
            Type = MongoDependencyYaml.YamlDiscriminator,
            UseReplicaSet = leftYaml.UseReplicaSet.GetValueOrDefault() || rightYaml.UseReplicaSet.GetValueOrDefault(),
        };
    }

    public Dependency ToDependencyModel(MongoDependencyYaml yaml)
    {
        return new MongoDependency
        {
            UseReplicaSet = yaml.UseReplicaSet.GetValueOrDefault()
        };
    }
}