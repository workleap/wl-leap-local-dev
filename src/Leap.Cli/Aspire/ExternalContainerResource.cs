namespace Leap.Cli.Aspire;

internal sealed class ExternalContainerResource(string name, string containerName) : Resource(name), IResourceWithWaitSupport
{
    public string ContainerName { get; } = containerName;

    public string ResourceType { get; init; } = "Container";

    public List<string> Urls { get; init; } = [];
}
