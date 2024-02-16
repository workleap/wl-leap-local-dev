namespace Leap.Cli.Model;

internal sealed class EventGridSettings
{
    public Dictionary<string, string> Topics { get; set; } = new();
}
