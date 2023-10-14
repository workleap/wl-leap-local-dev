using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Configuration;

internal interface ILeapYamlAccessor
{
    Task<LeapYaml[]> GetAllAsync(CancellationToken cancellationToken);
}