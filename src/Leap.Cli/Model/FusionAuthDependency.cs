namespace Leap.Cli.Model;

internal sealed class FusionAuthDependency : Dependency
{
    public const string DependencyType = "fusionauth";

    public FusionAuthDependency()
        : base(DependencyType, [new PostgresDependency()])
    {
    }
}