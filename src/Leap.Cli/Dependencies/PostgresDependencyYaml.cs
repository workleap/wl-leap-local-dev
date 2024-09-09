using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "postgres";
}