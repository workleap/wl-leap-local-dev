using System.Text.Json;
using System.Text.Json.Serialization;
using Leap.Cli.Configuration;

namespace Leap.Cli.Model;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(PreferencesSettings))]
internal sealed partial class LeapPreferencesGenerationContext : JsonSerializerContext
{
}