namespace Leap.Cli.Model;

internal sealed class Service
{
    private Runner? _activeRunner;

    public required string Name { get; init; }

    public List<Runner> Runners { get; } = [];

    public Runner ActiveRunner
    {
        get => this._activeRunner
            ?? this.Runners.FirstOrDefault()
            ?? throw new InvalidOperationException($"No runners are defined for the service '{this.Name}'");

        set => this._activeRunner = value;
    }

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Ingress Ingress { get; } = new();

    public Dictionary<string, string> GetServiceAndRunnerEnvironmentVariables()
    {
        var environmentVariables = new Dictionary<string, string>(
            capacity: this.EnvironmentVariables.Count + this.ActiveRunner.EnvironmentVariables.Count,
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

    public string GetUrl()
    {
        if (this.ActiveRunner is RemoteRunner remoteRunner)
        {
            return remoteRunner.Url;
        }

        return this.Ingress.Host.IsLocalhost
            ? $"{this.ActiveRunner.Protocol}://localhost:{this.Ingress.LocalhostPort}"
            : $"https://{this.Ingress.Host}:{Constants.LeapReverseProxyPort}{this.Ingress.Path.TrimEnd('/')}";
    }
}