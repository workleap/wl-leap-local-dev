namespace Leap.Cli.Configuration;

internal interface ILeapYamlAccessor
{
    Task<LeapYamlFile[]> GetAllAsync(CancellationToken cancellationToken);
}