namespace Leap.Cli.Model;

internal sealed class PostgresDependency : Dependency
{
    public const string DependencyType = "postgres";

    public PostgresDependency()
        : base(DependencyType)
    {
    }
}