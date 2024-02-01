namespace Leap.Cli.Model;

internal sealed class Service
{
    public string Name { get; set; } = string.Empty;

    public List<Binding> Bindings { get; } = new();

    public Binding? ActiveBinding { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public Ingress Ingress { get; } = new();
}