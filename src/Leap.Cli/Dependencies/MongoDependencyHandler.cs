using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyHandler : DependencyHandler<MongoDependency>
{
    protected override Task BeforeStartAsync(MongoDependency dependency, CancellationToken cancellationToken)
    {
        // TODO declare docker-compose.yml for MongoDB
        return Task.CompletedTask;
    }
}