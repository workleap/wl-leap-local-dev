using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed class AzuriteDependencyYamlHandler : IDependencyYamlHandler
{
    private const string ContainersKey = "containers";
    private const string TablesKey = "tables";
    private const string QueuesKey = "queues";

    private readonly ILogger _logger;

    public AzuriteDependencyYamlHandler(ILogger<AzuriteDependencyYamlHandler> logger)
    {
        this._logger = logger;
    }

    public bool CanHandle(string dependencyType)
    {
        return AzuriteDependency.DependencyType.Equals(dependencyType, StringComparison.OrdinalIgnoreCase);
    }

    public DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml) => new DependencyYaml
    {
        Type = AzuriteDependency.DependencyType,
        [ContainersKey] = new List<string>(GetContainers(leftYaml).Concat(GetContainers(rightYaml))),
        [TablesKey] = new List<string>(GetTables(leftYaml).Concat(GetTables(rightYaml))),
        [QueuesKey] = new List<string>(GetQueues(leftYaml).Concat(GetQueues(rightYaml))),
    };

    private static IEnumerable<string> GetContainers(DynamicObjectYaml yaml) => GetNonNullStringSequence(yaml, ContainersKey);

    private static IEnumerable<string> GetTables(DynamicObjectYaml yaml) => GetNonNullStringSequence(yaml, TablesKey);

    private static IEnumerable<string> GetQueues(DynamicObjectYaml yaml) => GetNonNullStringSequence(yaml, QueuesKey);

    private static IEnumerable<string> GetNonNullStringSequence(DynamicObjectYaml yaml, string key)
    {
        return (yaml.GetSequence(key) ?? Enumerable.Empty<object?>()).OfType<string>();
    }

    public Dependency ToDependencyModel(DependencyYaml yaml)
    {
        var containers = GetContainers(yaml).Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateContainerName).ToArray();
        var tables = GetTables(yaml).Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateTableName).ToArray();
        var queues = GetQueues(yaml).Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateQueueName).ToArray();

        return new AzuriteDependency(containers, tables, queues);
    }

    private bool ValidateContainerName(string containerName) => this.ValidateResourceName(containerName, AzuriteNameValidator.ValidateContainerName);

    private bool ValidateTableName(string tableName) => this.ValidateResourceName(tableName, AzuriteNameValidator.ValidateTableName);

    private bool ValidateQueueName(string queueName) => this.ValidateResourceName(queueName, AzuriteNameValidator.ValidateQueueName);

    private bool ValidateResourceName(string resourceName, Action<string> validator)
    {
        try
        {
            validator(resourceName);
            return true;
        }
        catch (ArgumentException ex)
        {
            this._logger.LogWarning("{Warning}", ex.Message);
            return false;
        }
    }
}