namespace Leap.Cli.Model;

internal sealed record EnvironmentVariable(string Name, string Value, EnvironmentVariableScope Scope);