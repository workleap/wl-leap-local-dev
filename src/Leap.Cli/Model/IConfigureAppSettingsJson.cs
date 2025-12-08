using System.Text.Json.Nodes;

namespace Leap.Cli.Model;

internal interface IConfigureAppSettingsJson
{
    JsonObject Configuration { get; }
}