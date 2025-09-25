using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class PostgresDependency() : Dependency(PostgresDependencyYaml.YamlDiscriminator)
{
    public string? ImageTag { get; init; }
}