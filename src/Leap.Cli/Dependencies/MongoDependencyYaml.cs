using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "mongo";
}