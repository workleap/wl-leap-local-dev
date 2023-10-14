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

    public async Task<LeapYaml[]> GetAllAsync(CancellationToken cancellationToken)
    {
        Stream stream;

        try
        {
            stream = this._fileSystem.File.OpenRead(ConfigurationConstants.SecondaryLeapYamlFileName);
        }
        catch (FileNotFoundException)
        {
            try
            {
                stream = this._fileSystem.File.OpenRead(ConfigurationConstants.LeapYamlFileName);
            }
            catch (FileNotFoundException ex)
            {
                return Array.Empty<LeapYaml>();
            }
        }

        await using (stream)
        {
            var leapYaml = await LeapYamlSerializer.DeserializeAsync(stream, cancellationToken);

            if (leapYaml != null)
            {
                return new[] { leapYaml };
            }
        }

        return Array.Empty<LeapYaml>();
    }
}