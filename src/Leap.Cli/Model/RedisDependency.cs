using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class RedisDependency() : Dependency(RedisDependencyYaml.YamlDiscriminator);