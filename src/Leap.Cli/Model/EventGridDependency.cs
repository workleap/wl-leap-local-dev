using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class EventGridDependency(EventGridTopics topics) : Dependency(EventGridDependencyYaml.YamlDiscriminator)
{
    public EventGridTopics Topics { get; } = topics;
}