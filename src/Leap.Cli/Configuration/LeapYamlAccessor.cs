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

    public Task<IEnumerable<LeapYaml>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var stream = this._fileSystem.File.OpenRead(ConfigurationConstants.LeapYamlFileName);
        var leapYaml = LeapYamlSerializer.Deserialize(stream);

        return Task.FromResult(new[] { leapYaml }.AsEnumerable());
    }
}