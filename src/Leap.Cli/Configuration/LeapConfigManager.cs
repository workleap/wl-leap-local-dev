using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;

namespace Leap.Cli.Configuration;

internal sealed class LeapConfigManager : ILeapYamlAccessor, IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger<LeapConfigManager> _logger;
    private readonly SemaphoreSlim _lock;

    private HashSet<string>? _leapYamlPaths;
    private LeapYamlFile[]? _cachedLeapYamls;

    public LeapConfigManager(IFileSystem fileSystem, IPlatformHelper platformHelper, ILogger<LeapConfigManager> logger)
    {
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._logger = logger;
        this._lock = new SemaphoreSlim(1, 1);
    }

    public void SetConfigurationFilesAsync(string[] configFilePaths)
    {
        if (configFilePaths.Length <= 0)
        {
            return;
        }

        this._leapYamlPaths = this.NormalizeFilePaths(configFilePaths);
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

    private async IAsyncEnumerable<LeapYamlFile> GetAllInternalAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (this._leapYamlPaths != null)
        {
            foreach (var yamlFilePath in this._leapYamlPaths)
            {
                LeapYaml? leapYaml = null;
                try
                {
                    await using var leapYamlStream = this._fileSystem.File.OpenRead(yamlFilePath);
                    leapYaml = await LeapYamlSerializer.DeserializeAsync(leapYamlStream, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' doesn't exist", yamlFilePath);
                }
                catch (IOException ex)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' couldn't be read: {Reason}", yamlFilePath, ex.Message);
                }
                catch (YamlException ex)
                {
                    this._logger.LogWarning("The leap YAML file '{LeapYamlPath}' is malformed: {Reason}", yamlFilePath, ex.Message);
                }

                if (leapYaml != null)
                {
                    yield return new LeapYamlFile(leapYaml, yamlFilePath);
                }
            }
        }
        else
        {
            var defaultConfig = await this.LoadFromCurrentDirectoryAsync(cancellationToken);

            if (defaultConfig != null)
            {
                yield return defaultConfig;
            }
        }
    }

    private async Task<LeapYamlFile?> LoadFromCurrentDirectoryAsync(CancellationToken cancellationToken)
    {
        FileSystemStream? stream = null;

        try
        {
            if (this._fileSystem.File.Exists(Constants.LeapYamlFileName))
            {
                stream = this._fileSystem.File.OpenRead(Constants.LeapYamlFileName);
            }
            else if (this._fileSystem.File.Exists(Constants.SecondaryLeapYamlFileName))
            {
                stream = this._fileSystem.File.OpenRead(Constants.SecondaryLeapYamlFileName);
            }
        }
        catch (IOException ex)
        {
            this._logger.LogWarning("The leap YAML file couldn't be read: {Reason}", ex.Message);
        }

        if (stream == null)
        {
            return null;
        }

        await using (stream)
        {
            try
            {
                var leapYaml = await LeapYamlSerializer.DeserializeAsync(stream, cancellationToken);

                if (leapYaml != null)
                {
                    return new LeapYamlFile(leapYaml, stream.Name);
                }
            }
            catch (YamlException ex)
            {
                this._logger.LogWarning("The leap YAML file is malformed: {Reason}", ex.Message);
            }
        }

        return null;
    }

    private HashSet<string> NormalizeFilePaths(string[] filePaths)
    {
        var comparer = this._platformHelper.CurrentOS == OSPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var uniquePaths = new HashSet<string>(comparer);

        foreach (var filePath in filePaths)
        {
            uniquePaths.Add(NormalizeFilePath(filePath));
        }

        return uniquePaths;
    }

    private static string NormalizeFilePath(string path)
    {
        if (path.StartsWith('~'))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..]);
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Environment.CurrentDirectory, path);
        }

        // Fix slashes
        // Based on: https://github.com/dotnet/aspire/blob/v8.0.0-preview.2.23619.3/src/Aspire.Hosting/Utils/PathNormalizer.cs
        path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        path = Path.GetFullPath(path);

        return path;
    }

    public void Dispose()
    {
        this._lock.Dispose();
    }
}
