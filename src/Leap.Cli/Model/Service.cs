namespace Leap.Cli.Model;

internal sealed class Service
{
    public string Name { get; set; } = string.Empty;

    public List<Runner> Runners { get; } = new();

    public Runner? ActiveRunner { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Ingress Ingress { get; } = new();
}