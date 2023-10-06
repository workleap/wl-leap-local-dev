namespace Leap.Cli.DockerCompose;

internal interface IConfigureEnvironmentVariables
{
    void Configure(Action<Dictionary<string, string>> configure);
}