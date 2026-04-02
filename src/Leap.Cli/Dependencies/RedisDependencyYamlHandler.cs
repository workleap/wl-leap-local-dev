using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyYamlHandler : IDependencyYamlHandler<RedisDependencyYaml>
{
    public RedisDependencyYaml Merge(RedisDependencyYaml leftYaml, RedisDependencyYaml rightYaml)
    {
        return new RedisDependencyYaml
        {
            Type = RedisDependencyYaml.YamlDiscriminator,
            Mcp = leftYaml.Mcp ?? rightYaml.Mcp,
        };
    }

    public Dependency ToDependencyModel(RedisDependencyYaml yaml)
    {
        return new RedisDependency
        {
            Mcp = yaml.Mcp ?? true,
        };
    }
}