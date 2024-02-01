using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.Json;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Configuration;

internal sealed class UserSettingsManager : IUserSettingsManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger _logger;

    public UserSettingsManager(IFileSystem fileSystem, IPlatformHelper platformHelper, ILogger<UserSettingsManager> logger)
    {
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._logger = logger;
    }

    public async Task<UserSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var settingsFileStream = this._fileSystem.File.OpenRead(Constants.LeapUserSettingsFilePath);
            return await JsonSerializer.DeserializeAsync(settingsFileStream, UserSettingsSourceGenerationContext.Default.UserSettings, cancellationToken);
        }
        catch (JsonException)
        {
            this._logger.LogWarning("The configuration file '{ConfigPath}' is malformed", Constants.LeapUserSettingsFilePath);
        }
        catch (FileNotFoundException)
        {
            // That's ok. Maybe the dev deleted it. We don't need a user settings file for Leap to work.
        }
        catch (DirectoryNotFoundException)
        {
            // That's ok. Maybe the dev deleted it. We don't need a user settings file for Leap to work.
        }
        catch (IOException ex)
        {
            this._logger.LogWarning("The configuration file '{ConfigPath}' couldn't be read: {Reason}", Constants.LeapUserSettingsFilePath, ex.Message);
        }

        return null;
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            this._fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(Constants.LeapUserSettingsFilePath)!);
            await using var settingsFileStream = this._fileSystem.File.Open(Constants.LeapUserSettingsFilePath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(settingsFileStream, settings, UserSettingsSourceGenerationContext.Default.UserSettings, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new LeapException($"The configuration file '{Constants.LeapUserSettingsFilePath}' couldn't be written: {ex.Message}", ex);
        }
    }

    public async Task AddLeapYamlFilePathAsync(string leapYamlFilePath, CancellationToken cancellationToken)
    {
        var settings = await this.LoadAsync(cancellationToken) ?? new UserSettings();

        var filePaths = this.NormalizeFilePaths(settings);
        filePaths.Add(NormalizeFilePath(leapYamlFilePath));
        settings.LeapYamlFilePaths = filePaths.ToArray();

        await this.SaveAsync(settings, cancellationToken);
    }

    public async Task RemoveLeapYamlFilePathAsync(string leapYamlFilePath, CancellationToken cancellationToken)
    {
        var settings = await this.LoadAsync(cancellationToken) ?? new UserSettings();

        var filePaths = this.NormalizeFilePaths(settings);
        filePaths.Remove(NormalizeFilePath(leapYamlFilePath));
        settings.LeapYamlFilePaths = filePaths.ToArray();

        await this.SaveAsync(settings, cancellationToken);
    }

    private HashSet<string> NormalizeFilePaths(UserSettings settings)
    {
        var comparer = this._platformHelper.CurrentOS == OSPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var filePaths = new HashSet<string>(comparer);

        if (settings.LeapYamlFilePaths == null)
        {
            return filePaths;
        }

        foreach (var filePath in settings.LeapYamlFilePaths)
        {
            if (filePath != null)
            {
                filePaths.Add(NormalizeFilePath(filePath));
            }
        }

        return filePaths;
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
}