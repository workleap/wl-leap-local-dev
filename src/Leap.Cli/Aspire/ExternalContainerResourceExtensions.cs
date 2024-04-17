using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal static class ExternalContainerResourceExtensions
{
    public static IResourceBuilder<ExternalContainerResource> AddExternalContainer(this IDistributedApplicationBuilder builder, string name, string containerNameOrId)
    {
        builder.Services.TryAddLifecycleHook<ExternalContainerResourceLifecycleHook>();

        return builder.AddResource(new ExternalContainerResource(name, containerNameOrId))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Leap Dependency",
                Properties = []
            })
            .ExcludeFromManifest();
    }
}
