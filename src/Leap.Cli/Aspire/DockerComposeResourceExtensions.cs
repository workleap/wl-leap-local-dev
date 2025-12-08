using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal static class DockerComposeResourceExtensions
{
    public static IResourceBuilder<DockerComposeResource> AddDockerComposeResource(this IDistributedApplicationBuilder builder, DockerComposeResource resource)
    {
        builder.Services.TryAddEventingSubscriber<DockerComposeResourceLifecycleHook>();

        return builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = resource.ResourceType,
                CreationTimeStamp = DateTime.Now,
                State = resource.InitialState,
                Properties = [],
            });
    }
}