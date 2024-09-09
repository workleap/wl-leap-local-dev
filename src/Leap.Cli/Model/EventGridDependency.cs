namespace Leap.Cli.Model;

internal sealed class EventGridDependency(EventGridTopics topics) : Dependency
{
    public EventGridTopics Topics { get; } = topics;
}