namespace Leap.Cli.Model;

internal sealed class SqlServerDependency : Dependency
{
    public const string DependencyType = "sqlserver";

    public SqlServerDependency()
        : base(DependencyType)
    {
    }
}