namespace Leap.Cli.Model;

internal sealed class EventGridTopics()
    : Dictionary<string, EventGridSubscriptions>(StringComparer.OrdinalIgnoreCase)
{
    public EventGridTopics DeepClone()
    {
        var clone = new EventGridTopics();

        foreach (var (topicName, subscriptions) in this)
        {
            clone[topicName] = new EventGridSubscriptions(subscriptions);
        }

        return clone;
    }

    public void Merge(Dictionary<string, string?[]?>? topics)
    {
        if (topics == null)
        {
            return;
        }

        foreach (var (topicName, subscriptions) in topics)
        {
            if (subscriptions == null)
            {
                continue;
            }

            if (this.TryGetValue(topicName, out var existingSubscriptions))
            {
                foreach (var subscription in subscriptions)
                {
                    if (subscription != null)
                    {
                        existingSubscriptions.Add(subscription);
                    }
                }
            }
            else
            {
                this[topicName] = new EventGridSubscriptions(subscriptions.OfType<string>());
            }
        }
    }
}