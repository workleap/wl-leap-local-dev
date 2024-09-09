using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class SqlServerDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "sqlserver";
}