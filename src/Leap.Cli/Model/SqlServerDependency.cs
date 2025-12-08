using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class SqlServerDependency() : Dependency(SqlServerDependencyYaml.YamlDiscriminator);