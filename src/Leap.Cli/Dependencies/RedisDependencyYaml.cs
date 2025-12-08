using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "redis";
}