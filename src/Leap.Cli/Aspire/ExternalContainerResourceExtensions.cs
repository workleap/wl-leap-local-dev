using System.Collections.Immutable;
using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal static class ExternalContainerResourceExtensions
{
    public static IResourceBuilder<ExternalContainerResource> AddExternalContainer(this IDistributedApplicationBuilder builder, ExternalContainerResource resource)
    {
        builder.Services.TryAddLifecycleHook<ExternalContainerResourceLifecycleHook>();

        var urls = resource.Urls
            .Select((url, index) => new UrlSnapshot(index.ToString(), url, IsInternal: false))
            .ToImmutableArray();

        return builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = resource.ResourceType,
                Properties = [],
                Urls = [.. urls],
            });
    }
}
