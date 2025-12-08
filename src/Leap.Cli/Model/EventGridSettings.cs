using System.Text.Json.Serialization;

namespace Leap.Cli.Model;

internal sealed class EventGridSettings
{
    [JsonConstructor]
    public EventGridSettings()
    {
    }

    public EventGridSettings(EventGridTopics topics)
    {
        foreach (var (topicName, subscriptions) in topics)
        {
            this.Topics[topicName] = [.. subscriptions];
        }
    }

    [JsonPropertyName("Topics")]
    public Dictionary<string, string?[]?> Topics { get; set; } = [];
}