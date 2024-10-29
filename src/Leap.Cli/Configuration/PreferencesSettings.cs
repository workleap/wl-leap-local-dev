using System.Text.Json.Serialization;

namespace Leap.Cli.Configuration;

internal sealed class PreferencesSettings
{
    [JsonPropertyName("services")]
    public Dictionary<string, Preference> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class Preference(string preferredRunner)
{
    [JsonPropertyName("preferredRunner")]
    public string PreferredRunner { get; set; } = preferredRunner;
}