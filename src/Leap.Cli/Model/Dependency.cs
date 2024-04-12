namespace Leap.Cli.Model;

internal abstract class Dependency : IEquatable<Dependency>
{
    protected Dependency(string type) : this(type, [])
    {
    }

    protected Dependency(string type, IReadOnlyCollection<Dependency> dependencies)
    {
        this.Type = type;
        this.Dependencies = dependencies;
    }

    public IReadOnlyCollection<Dependency> Dependencies { get; }

    public string Type { get; }

    public bool Equals(Dependency? other)
    {
        return this.Type == other?.Type;
    }

    public override int GetHashCode()
    {
        return this.Type.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as Dependency);
    }
}