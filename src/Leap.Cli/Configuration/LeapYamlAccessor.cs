using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using Leap.Cli.Configuration.Yaml;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;

namespace Leap.Cli.Configuration;

internal sealed class LeapYamlAccessor : ILeapYamlAccessor, IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly IUserSettingsManager _userSettingsManager;
    private readonly ILogger<LeapYamlAccessor> _logger;
    private readonly SemaphoreSlim _lock;

    private LeapYamlFile[]? _cachedLeapYamls;

    public LeapYamlAccessor(IFileSystem fileSystem, IUserSettingsManager userSettingsManager, ILogger<LeapYamlAccessor> logger)
    {
        this._fileSystem = fileSystem;
        this._userSettingsManager = userSettingsManager;
        this._logger = logger;
        this._lock = new SemaphoreSlim(1, 1);
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "This is a classic double-checked lock pattern.")]
    public async Task<LeapYamlFile[]> GetAllAsync(CancellationToken cancellationToken)
    {
        if (this._cachedLeapYamls != null)
        {
            return this._cachedLeapYamls;
        }

        await this._lock.WaitAsync(cancellationToken);

        try
        {
            if (this._cachedLeapYamls != null)
            {
                return this._cachedLeapYamls;
            }

            var cachedLeapYamls = new List<LeapYamlFile>();

            await foreach (var leapYaml in this.GetAllInternalAsync(cancellationToken))
            {
                cachedLeapYamls.Add(leapYaml);
            }

            this._cachedLeapYamls = cachedLeapYamls.ToArray();
        }
        finally
        {
            this._lock.Release();
        }

        return this._cachedLeapYamls;
    }

    // TODO refactor the method? It's a bit too long and complex
    private async IAsyncEnumerable<LeapYamlFile> GetAllInternalAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // TODO accessing the settings might be done from elsewhere in the future, so extract it?
        var settings = await this._userSettingsManager.LoadAsync(cancellationToken);

        if (settings is { LeapYamlFilePaths: { } yamlFilePaths })
        {
            foreach (var yamlFilePath in yamlFilePaths)
            {
                if (yamlFilePath == null)
                {
                    continue;
                }

                LeapYaml? leapYaml = null;
                try
                {
                    await using var leapYamlStream = this._fileSystem.File.OpenRead(yamlFilePath);
                    leapYaml = await LeapYamlSerializer.DeserializeAsync(leapYamlStream, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' specified in the configuration '{ConfigPath}' doesn't exist", yamlFilePath, Constants.LeapUserSettingsFilePath);
                }
                catch (IOException ex)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' specified in the configuration '{ConfigPath}' couldn't be read: {Reason}", yamlFilePath, Constants.LeapUserSettingsFilePath, ex.Message);
                }
                catch (YamlException ex)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' specified in the configuration '{ConfigPath}' is malformed: {Reason}", yamlFilePath, Constants.LeapUserSettingsFilePath, ex.Message);
                }

                if (leapYaml != null)
                {
                    yield return new LeapYamlFile(leapYaml, yamlFilePath);
                }
            }
        }

        // TODO Consider using File.Exists instead of catching FileNotFoundException? Still use streams though
        // TODO Consider scanning parent directories for leap.yaml files? Kind of like .gitignore and global.json files
        FileSystemStream? stream = null;
        try
        {
            stream = this._fileSystem.File.OpenRead(Constants.SecondaryLeapYamlFileName);
        }
        catch (FileNotFoundException)
        {
            try
            {
                stream = this._fileSystem.File.OpenRead(Constants.LeapYamlFileName);
            }
            catch (FileNotFoundException)
            {
            }
            catch (IOException ex)
            {
                this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' couldn't be read: {Reason}", Constants.LeapYamlFileName, ex.Message);
            }
        }
        catch (IOException ex)
        {
            this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' couldn't be read: {Reason}", Constants.SecondaryLeapYamlFileName, ex.Message);
        }

        if (stream == null)
        {
            yield break;
        }

        await using (stream)
        {
            var leapYaml = await LeapYamlSerializer.DeserializeAsync(stream, cancellationToken);

            if (leapYaml != null)
            {
                // TODO consider adding an enum flag to distinguish yaml files specified in the config file VS those found in the current dir
                yield return new LeapYamlFile(leapYaml, stream.Name);
            }
        }
    }

    public void Dispose()
    {
        this._lock.Dispose();
    }
}