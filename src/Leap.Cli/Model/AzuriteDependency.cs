using Leap.Cli.Dependencies.Azurite;

namespace Leap.Cli.Model;

internal sealed class AzuriteDependency : Dependency
{
    public AzuriteDependency(IReadOnlyCollection<string> containers, IReadOnlyCollection<string> tables, IReadOnlyCollection<string> queues) : base(AzuriteDependencyYaml.YamlDiscriminator)
    {
        this.Containers = containers;
        this.Tables = tables;
        this.Queues = queues;
    }

    public IReadOnlyCollection<string> Containers { get; }

    public IReadOnlyCollection<string> Tables { get; }

    public IReadOnlyCollection<string> Queues { get; }
}