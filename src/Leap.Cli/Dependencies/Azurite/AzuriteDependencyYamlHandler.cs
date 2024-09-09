using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed class AzuriteDependencyYamlHandler(ILogger<AzuriteDependencyYamlHandler> logger)
    : IDependencyYamlHandler<AzuriteDependencyYaml>
{
    public AzuriteDependencyYaml Merge(AzuriteDependencyYaml leftYaml, AzuriteDependencyYaml rightYaml) => new()
    {
        Type = AzuriteDependencyYaml.YamlDiscriminator,
        Containers = [.. leftYaml.Containers ?? [], .. rightYaml.Containers ?? []],
        Tables = [.. leftYaml.Tables ?? [], .. rightYaml.Tables ?? []],
        Queues = [.. leftYaml.Queues ?? [], .. rightYaml.Queues ?? []]
    };

    public Dependency ToDependencyModel(AzuriteDependencyYaml yaml)
    {
        var containers = yaml.Containers?.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateContainerName).ToArray() ?? [];
        var tables = yaml.Tables?.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateTableName).ToArray() ?? [];
        var queues = yaml.Queues?.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Where(this.ValidateQueueName).ToArray() ?? [];

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
            logger.LogWarning("{Warning}", ex.Message);
            return false;
        }
    }
}