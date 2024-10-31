using Leap.Cli.Configuration;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class PreferencesRemoveCommand : Command<PreferencesRemoveCommandOptions, PreferencesRemoveCommandHandler>
{
    public PreferencesRemoveCommand() : base("remove", "Remove preferences for Leap services")
    {
        this.AddOption(PreferencesOptions.CreateServiceOption());
    }
}

internal sealed class PreferencesRemoveCommandOptions : ICommandOptions
{
    public string Service { get; init; } = string.Empty;
}

internal sealed class PreferencesRemoveCommandHandler(PreferencesSettingsManager preferencesSettingsManager, ILogger<PreferencesRemoveCommand> logger)
    : ICommandOptionsHandler<PreferencesRemoveCommandOptions>
{
    public async Task<int> HandleAsync(PreferencesRemoveCommandOptions options, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackPreferencesRun();

        if (await preferencesSettingsManager.RemovePreferredRunnerForServiceAsync(options.Service, cancellationToken))
        {
            logger.LogInformation("Removed preference for service: {Service}", options.Service);
        }
        else
        {
            logger.LogWarning("No preference found for service: {Service}", options.Service);
        }

        return 0;
    }
}