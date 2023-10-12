namespace Leap.Cli.Model;

internal abstract class Dependency
{
    protected Dependency(string type)
    {
        this.Type = type;
    }

    public string Type { get; }
}