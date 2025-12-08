using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class EventGridDependencyYamlHandler : IDependencyYamlHandler<EventGridDependencyYaml>
{
    public EventGridDependencyYaml Merge(EventGridDependencyYaml leftYaml, EventGridDependencyYaml rightYaml)
    {
        var topics = new Dictionary<string, string?[]?>(StringComparer.OrdinalIgnoreCase);

        MergeTopics(topics, leftYaml);
        MergeTopics(topics, rightYaml);

        return new EventGridDependencyYaml
        {
            Type = EventGridDependencyYaml.YamlDiscriminator,
            Topics = topics
        };
    }

    private static void MergeTopics(Dictionary<string, string?[]?> topics, EventGridDependencyYaml yaml)
    {
        if (yaml.Topics == null)
        {
            return;
        }

        foreach (var (topicName, currentSubscriptions) in yaml.Topics)
        {
            if (currentSubscriptions == null)
            {
                continue;
            }

            if (topics.TryGetValue(topicName, out var existingSubscriptions) && existingSubscriptions != null)
            {
                topics[topicName] = [.. existingSubscriptions, .. currentSubscriptions];
            }
            else
            {
                topics[topicName] = currentSubscriptions;
            }
        }
    }

    public Dependency ToDependencyModel(EventGridDependencyYaml yaml)
    {
        var eventGridTopics = new EventGridTopics();

        if (yaml.Topics != null)
        {
            foreach (var (topicName, subscriptions) in yaml.Topics)
            {
                if (subscriptions != null)
                {
                    eventGridTopics[topicName] = new EventGridSubscriptions(subscriptions.OfType<string>());
                }
            }
        }

        return new EventGridDependency(eventGridTopics);
    }
}