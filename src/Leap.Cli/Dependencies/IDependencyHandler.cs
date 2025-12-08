using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal interface IDependencyHandler
{
    Task HandleAsync(Dependency dependency, CancellationToken cancellationToken);
}