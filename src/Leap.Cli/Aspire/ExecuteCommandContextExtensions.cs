using Microsoft.Extensions.DependencyInjection;

namespace Leap.Cli.Aspire;

public static class ExecuteCommandContextExtensions
{
    public static async Task TriggerResourceSnapshotChangeAsync(this ExecuteCommandContext context, IResource resource)
    {
        await context.ServiceProvider.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(resource, state => state with
        {
            // No-op. We're not mutating the snapshot, we only want to trigger a change to force a re-evaluation (updateState lambdas declared above)
            // of the commands displayed on the dashboard
        });
    }
}