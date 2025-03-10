using System.Security.Cryptography;
using System.Text;
using Leap.Cli.Configuration;

namespace Leap.Cli.Model;

internal sealed class Service
{
    private Runner? _activeRunner;

    public Service(string name, LeapYamlFile leapYaml)
    {
        this.Name = name;

        var filePathHash = SHA256.HashData(Encoding.UTF8.GetBytes(leapYaml.Path));
        var containerNameSuffix = Convert.ToHexString(filePathHash).ToLowerInvariant()[..8];
        this.ContainerName = $"{this.Name.ToLowerInvariant()}-{containerNameSuffix}";
    }

    public string Name { get; }

    public string ContainerName { get; }

    public string? HealthCheckPath { get; set; }

    public string PreferredRunner { get; set; } = string.Empty;

    public List<Runner> Runners { get; } = [];

    public HashSet<string> Profiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Runner ActiveRunner
    {
        get => this._activeRunner
               ?? this.Runners.FirstOrDefault()
               ?? throw new InvalidOperationException($"No runners are defined for the service '{this.Name}'");

        set => this._activeRunner = value;
    }

    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Ingress Ingress { get; } = new();

    public string ReverseProxyUrl => $"https://{this.Ingress.Host}:{Constants.LeapReverseProxyPort}{this.Ingress.Path.TrimEnd('/')}";

    public string LocalhostUrl => $"{this.ActiveRunner.Protocol}://localhost:{this.Ingress.LocalhostPort}";

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

    public string GetPrimaryUrl()
    {
        if (this.ActiveRunner is RemoteRunner remoteRunner)
        {
            return remoteRunner.Url;
        }

        return this.Ingress.Host.IsLocalhost ? this.LocalhostUrl : this.ReverseProxyUrl;
    }

    public List<string> GetRunnerNames()
    {
        return this.Runners
            .Select(runner => runner.Type)
            .ToList();
    }

    public Uri? GetHealthCheckUrl()
    {
        if (this.HealthCheckPath == null)
        {
            return null;
        }

        return new Uri($"{this.GetPrimaryUrl().TrimEnd('/')}/{this.HealthCheckPath.TrimStart('/')}", UriKind.Absolute);
    }
}