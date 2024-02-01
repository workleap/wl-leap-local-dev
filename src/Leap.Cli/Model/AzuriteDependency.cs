namespace Leap.Cli.Model;

internal sealed class AzuriteDependency : Dependency
{
    public const string DependencyType = "azurite";

    public AzuriteDependency(IReadOnlyCollection<string> containers, IReadOnlyCollection<string> tables, IReadOnlyCollection<string> queues)
        : base(DependencyType)
    {
        this.Containers = containers;
        this.Tables = tables;
        this.Queues = queues;
    }

    public IReadOnlyCollection<string> Containers { get; }

    public IReadOnlyCollection<string> Tables { get; }

    public IReadOnlyCollection<string> Queues { get; }
}