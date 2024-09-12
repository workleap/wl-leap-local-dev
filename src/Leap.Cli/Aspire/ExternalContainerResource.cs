namespace Leap.Cli.Aspire;

internal sealed class ExternalContainerResource(string name, string containerNameOrId) : Resource(name)
{
    public string ContainerNameOrId { get; } = containerNameOrId;

    public string ResourceType { get; init; } = "Container";

    public List<string> Urls { get; init; } = [];
}
