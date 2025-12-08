using System.Text.Json;
using System.Text.Json.Serialization;

namespace Leap.Cli.Model;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(EventGridSettings))]
internal sealed partial class EventGridSettingsSourceGenerationContext : JsonSerializerContext
{
}
