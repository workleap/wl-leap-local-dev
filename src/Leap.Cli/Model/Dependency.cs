namespace Leap.Cli.Model;

internal abstract class Dependency(string name)
{
    public string Name { get; } = name;
}