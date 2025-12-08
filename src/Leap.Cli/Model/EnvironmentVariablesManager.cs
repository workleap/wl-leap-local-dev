namespace Leap.Cli.Model;

internal sealed class EnvironmentVariablesManager : IConfigureEnvironmentVariables, IEnvironmentVariableManager
{
    private readonly List<Action<List<EnvironmentVariable>>> _configurations;

    public EnvironmentVariablesManager()
    {
        this._configurations = new List<Action<List<EnvironmentVariable>>>();
    }

    public List<EnvironmentVariable> EnvironmentVariables => this.GenerateEnvironmentVariables();

    public void Configure(Action<List<EnvironmentVariable>> configure)
    {
        this._configurations.Add(configure);
    }

    private List<EnvironmentVariable> GenerateEnvironmentVariables()
    {
        var environmentVariables = new List<EnvironmentVariable>();

        foreach (var configuration in this._configurations)
        {
            configuration(environmentVariables);
        }

        return environmentVariables;
    }
}