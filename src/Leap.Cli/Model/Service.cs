namespace Leap.Cli.Model;

internal sealed class Service
{
    public string Name { get; set; } = string.Empty;

    public List<Runner> Runners { get; } = new();

    public Runner? ActiveRunner { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Ingress Ingress { get; } = new();

    public Dictionary<string, string> GetServiceAndRunnerEnvironmentVariables()
    {
        var environmentVariables = new Dictionary<string, string>(
            capacity: this.EnvironmentVariables.Count + this.ActiveRunner!.EnvironmentVariables.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in this.EnvironmentVariables)
        {
            environmentVariables[key] = value;
        }

        foreach (var (key, value) in this.ActiveRunner.EnvironmentVariables)
        {
            environmentVariables[key] = value;
        }

        return environmentVariables;
    }
}