namespace Leap.Cli.Aspire;

internal sealed class DockerComposeResource(string name, string containerName) : Resource(name), IResourceWithWaitSupport
{
    public string ContainerName { get; } = containerName;

    public string ResourceType { get; init; } = "Container";

    public string InitialState { get; init; } = KnownResourceStates.Starting;

    public List<string> Urls { get; init; } = [];
}