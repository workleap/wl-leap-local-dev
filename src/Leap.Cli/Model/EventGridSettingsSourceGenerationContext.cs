using System.Text.Json.Serialization;

namespace Leap.Cli.Model;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(EventGridSettings))]
internal sealed partial class EventGridSettingsSourceGenerationContext : JsonSerializerContext
{
}
