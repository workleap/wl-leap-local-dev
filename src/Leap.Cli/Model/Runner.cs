namespace Leap.Cli.Model;

internal abstract class Runner
{
    public abstract int? Port { get; set; }

    public string? Protocol { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public override string ToString()
    {
        return this.GetType().Name.Replace("Runner", "").ToLowerInvariant();
    }
}
