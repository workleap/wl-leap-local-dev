using Aspire.Hosting.Eventing;

internal static class DistributedApplicationEventingExtensions
{
    public static DistributedApplicationEventSubscription Subscribe<T>(this IDistributedApplicationEventing eventing, string resourceName, Func<T, CancellationToken, Task> callback)
        where T : IDistributedApplicationResourceEvent
    {
        return eventing.Subscribe<T>(async (evt, cancellationToken) =>
        {
            if (evt.Resource.Name == resourceName)
            {
                await callback(evt, cancellationToken);
            }
        });
    }
}