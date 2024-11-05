using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal static class DockerComposeResourceExtensions
{
    public static IResourceBuilder<DockerComposeResource> AddDockerComposeResource(this IDistributedApplicationBuilder builder, DockerComposeResource resource)
    {
        builder.Services.TryAddLifecycleHook<DockerComposeResourceLifecycleHook>();

        return builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = resource.ResourceType,
                CreationTimeStamp = DateTime.Now,
                State = KnownResourceStates.Starting,
                Properties = [],
            });
    }
}
