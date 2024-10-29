using System.IO.Abstractions;
using System.Text.Json;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Configuration;

internal sealed class PreferencesSettingsManager(IFileSystem fileSystem, ILogger<PreferencesSettingsManager> logger)
{
    public async Task<PreferencesSettings> GetUserLeapPreferencesAsync(CancellationToken cancellationToken)
    {
        fileSystem.Directory.CreateDirectory(Constants.RootDirectoryPath);
        EnsureFileIsNotDirectory(Constants.UserLeapPreferencesFilePath);
        if (File.Exists(Constants.UserLeapPreferencesFilePath))
        {
            try
            {
                await using var stream = new FileStream(Constants.UserLeapPreferencesFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return await JsonSerializer.DeserializeAsync<PreferencesSettings>(stream, cancellationToken: cancellationToken) ?? new PreferencesSettings();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "An error occured while reading the {UserLeapPreferencesFilePath}.",
                    Constants.UserLeapPreferencesFilePath);
            }
        }

        return new PreferencesSettings();
    }

    public async Task SaveUserLeapPreferencesAsync(PreferencesSettings preferences, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(Constants.UserLeapPreferencesFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, preferences, LeapPreferencesGenerationContext.Default.PreferencesSettings, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "An error occured while updating the {UserLeapPreferencesFilePath}.",
                Constants.UserLeapPreferencesFilePath);
        }
    }

    private static void EnsureFileIsNotDirectory(string filePath)
    {
        try
        {
            Directory.Delete(filePath, recursive: true);
        }
        catch
        {
            // Happens in the happy path when we already created the file and it is not a directory
        }
    }
}