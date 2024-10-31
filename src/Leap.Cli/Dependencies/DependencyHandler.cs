using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal abstract class DependencyHandler<TDependency> : IDependencyHandler
    where TDependency : Dependency
{
    public Task HandleAsync(Dependency dependency, CancellationToken cancellationToken)
    {
        return dependency is TDependency typedDependency ? this.HandleAsync(typedDependency, cancellationToken) : Task.CompletedTask;
    }

    protected virtual Task HandleAsync(TDependency dependency, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}