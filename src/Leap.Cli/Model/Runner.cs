namespace Leap.Cli.Model;

internal abstract class Runner(string type)
{
    public int? Port { get; set; }

    public string Protocol { get; set; } = "http";

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Type { get; } = type;

    public override string ToString()
    {
        return this.GetType().Name.Replace("Runner", "").ToLowerInvariant();
    }
}