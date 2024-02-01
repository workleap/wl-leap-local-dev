using System.Text.Json.Serialization;

namespace Leap.Cli.Configuration;

internal sealed class UserSettings
{
    [JsonPropertyName("leapFiles")]
    public string?[]? LeapYamlFilePaths { get; set; }
}