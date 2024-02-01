namespace Leap.Cli.Model;

internal interface IConfigureEnvironmentVariables
{
    void Configure(Action<List<EnvironmentVariable>> configure);
}