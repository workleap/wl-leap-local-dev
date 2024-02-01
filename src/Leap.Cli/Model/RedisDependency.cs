namespace Leap.Cli.Model;

internal sealed class RedisDependency : Dependency
{
    public const string DependencyType = "redis";

    public RedisDependency()
        : base(DependencyType)
    {
    }
}