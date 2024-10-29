using Leap.Cli.Configuration;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class PreferencesListCommand() : Command<PreferencesListCommandOptions, PreferencesListCommandHandler>("list", "List preferences for Leap services");

internal sealed class PreferencesListCommandOptions : ICommandOptions;

internal sealed class PreferencesListCommandHandler(PreferencesSettingsManager preferencesSettingsManager, ILogger<PreferencesSetCommand> logger)
    : ICommandOptionsHandler<PreferencesListCommandOptions>
{
    public async Task<int> HandleAsync(PreferencesListCommandOptions options, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackPreferencesRun();

        var preferences = await preferencesSettingsManager.GetUserLeapPreferencesAsync(cancellationToken);
        if (preferences.Services.Count == 0)
        {
            logger.LogInformation("No preferences were defined for any service.");
        }
        else
        {
            logger.LogInformation("Listing preferences for Leap services:");
            foreach (var (service, preference) in preferences.Services)
            {
                logger.LogInformation("- {Service}: `{PreferredRunner}` runner", service, preference.PreferredRunner);
            }
        }

        return 0;
    }
}