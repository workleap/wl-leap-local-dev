using System.Text.Json.Serialization;

namespace Leap.Cli.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class UserSettingsSourceGenerationContext : JsonSerializerContext
{
}