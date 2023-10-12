namespace Leap.Cli.Model;

internal sealed class MongoDependency : Dependency
{
    public const string DependencyType = "mongo";

    public MongoDependency()
        : base(DependencyType)
    {
    }
}