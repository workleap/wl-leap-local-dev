using System.IO.Abstractions;
using System.Text.Json;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Configuration;

internal sealed class PreferencesSettingsManager(IFileSystem fileSystem, ILogger<PreferencesSettingsManager> logger)
{
    public async Task<PreferencesSettings> GetLeapUserPreferencesAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(Constants.LeapUserPreferencesFilePath))
        {
            try
            {
                await using var stream = File.OpenRead(Constants.LeapUserPreferencesFilePath);
                var tempPreferences = await JsonSerializer.DeserializeAsync<PreferencesSettings>(stream, cancellationToken: cancellationToken) ?? new PreferencesSettings();
                return new PreferencesSettings(tempPreferences.Services);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "An error occurred while reading the '{LeapUserPreferencesFilePath}' file. Please make sure it's valid JSON or delete it.",
                    Constants.LeapUserPreferencesFilePath);
            }
        }

        return new PreferencesSettings();
    }

    public async Task SetPreferredRunnerForServiceAsync(string serviceName, string preferredRunner, CancellationToken cancellationToken)
    {
        var preferences = await this.GetLeapUserPreferencesAsync(cancellationToken);
        preferences.Services[serviceName] = new Preference(preferredRunner);
        await this.SaveLeapUserPreferencesAsync(preferences, cancellationToken);
    }

    public async Task<bool> RemovePreferredRunnerForServiceAsync(string serviceName, CancellationToken cancellationToken)
    {
        var preferences = await this.GetLeapUserPreferencesAsync(cancellationToken);
        var isRemoved = preferences.Services.Remove(serviceName);
        await this.SaveLeapUserPreferencesAsync(preferences, cancellationToken);
        return isRemoved;
    }

    private async Task SaveLeapUserPreferencesAsync(PreferencesSettings preferences, CancellationToken cancellationToken)
    {
        fileSystem.Directory.CreateDirectory(Constants.RootDirectoryPath);

        try
        {
            await using var stream = new FileStream(Constants.LeapUserPreferencesFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, preferences, LeapPreferencesGenerationContext.Default.PreferencesSettings, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "An error occurred while updating the '{LeapUserPreferencesFilePath}' file. Please make sure it's valid JSON or delete it.",
                Constants.LeapUserPreferencesFilePath);
        }
    }
}