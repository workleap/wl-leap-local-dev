namespace Leap.Cli.Model;

internal interface IEnvironmentVariableManager
{
    List<EnvironmentVariable> EnvironmentVariables { get; }
}