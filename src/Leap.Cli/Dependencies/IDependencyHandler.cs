using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal interface IDependencyHandler
{
    Task BeforeStartAsync(Dependency dependency, CancellationToken cancellationToken);

    Task AfterStartAsync(Dependency dependency, CancellationToken cancellationToken);
}