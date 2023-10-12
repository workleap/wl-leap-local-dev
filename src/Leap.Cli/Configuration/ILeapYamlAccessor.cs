using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Configuration;

internal interface ILeapYamlAccessor
{
    Task<IEnumerable<LeapYaml>> GetAllAsync(CancellationToken cancellationToken);
}