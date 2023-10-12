using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal abstract class DependencyHandler<TDependency> : IDependencyHandler
    where TDependency : Dependency
{
    public Task BeforeStartAsync(Dependency dependency, CancellationToken cancellationToken)
    {
        return dependency is TDependency typedDependency ? this.BeforeStartAsync(typedDependency, cancellationToken) : Task.CompletedTask;
    }

    public Task AfterStartAsync(Dependency dependency, CancellationToken cancellationToken)
    {
        return dependency is TDependency typedDependency ? this.AfterStartAsync(typedDependency, cancellationToken) : Task.CompletedTask;
    }

    protected virtual Task BeforeStartAsync(TDependency dependency, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterStartAsync(TDependency dependency, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}