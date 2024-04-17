using Aspire.Hosting.ApplicationModel;

namespace Leap.Cli.Aspire;

internal sealed class ExternalContainerResource(string name, string containerNameOrId) : Resource(name)
{
    public string ContainerNameOrId { get; } = containerNameOrId;
}
