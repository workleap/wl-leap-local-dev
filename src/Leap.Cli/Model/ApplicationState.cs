namespace Leap.Cli.Model;

internal sealed class ApplicationState
{
    public List<Dependency> Dependencies { get; } = new();

    public Dictionary<string, Service> Services { get; } = new();
}