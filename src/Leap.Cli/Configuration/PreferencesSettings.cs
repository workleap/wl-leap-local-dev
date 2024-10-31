using System.Text.Json.Serialization;

namespace Leap.Cli.Configuration;

internal sealed class PreferencesSettings
{
    [JsonPropertyName("services")]
    public Dictionary<string, Preference> Services { get; init; }

    [JsonConstructor]
    public PreferencesSettings()
    {
        this.Services = new Dictionary<string, Preference>(StringComparer.OrdinalIgnoreCase);
    }

    public PreferencesSettings(Dictionary<string, Preference> services)
    {
        this.Services = new Dictionary<string, Preference>(services, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetPreferredRunnerForService(string serviceName)
    {
        return this.Services.GetValueOrDefault(serviceName)?.PreferredRunner;
    }
}

internal sealed class Preference(string preferredRunner)
{
    [JsonPropertyName("preferredRunner")]
    public string PreferredRunner { get; set; } = preferredRunner;
}