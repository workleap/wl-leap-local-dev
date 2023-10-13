using System.IO.Abstractions;
using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Configuration;

internal sealed class LeapYamlAccessor : ILeapYamlAccessor
{
    private readonly IFileSystem _fileSystem;

    public LeapYamlAccessor(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;
    }

    public async Task<IEnumerable<LeapYaml>> GetAllAsync(CancellationToken cancellationToken)
    {
        var leapConfigs = new List<LeapYaml>();

        await using var stream = this._fileSystem.File.OpenRead(ConfigurationConstants.LeapYamlFileName);
        var leapYaml = await LeapYamlSerializer.DeserializeAsync(stream, cancellationToken);

        if (leapYaml != null)
        {
            leapConfigs.Add(leapYaml);
        }

        return leapConfigs.AsEnumerable();
    }
}