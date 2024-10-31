using System.Collections.Immutable;
using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal static class DockerComposeResourceExtensions
{
    public static IResourceBuilder<DockerComposeResource> AddDockerComposeResource(this IDistributedApplicationBuilder builder, DockerComposeResource resource)
    {
        builder.Services.TryAddLifecycleHook<DockerComposeResourceLifecycleHook>();

        var urls = resource.Urls
            .Select((url, index) => new UrlSnapshot(index.ToString(), url, IsInternal: false))
            .ToImmutableArray();

        return builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = resource.ResourceType,
                CreationTimeStamp = DateTime.Now,
                State = KnownResourceStates.Starting,
                Properties = [],
                Urls = [.. urls],
            });
    }
}
