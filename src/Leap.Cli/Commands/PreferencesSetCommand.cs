using Leap.Cli.Configuration;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class PreferencesSetCommand : Command<PreferencesSetCommandOptions, PreferencesSetCommandHandler>
{
    public PreferencesSetCommand() : base("set", "Set preferences for Leap services")
    {
        this.AddOption(PreferencesOptions.CreateServiceOption());
        this.AddOption(PreferencesOptions.CreateRunnerOption());
    }
}

internal sealed class PreferencesSetCommandOptions : ICommandOptions
{
    public string Service { get; init; } = string.Empty;
    public string Runner { get; init; } = string.Empty;
}

internal sealed class PreferencesSetCommandHandler(PreferencesSettingsManager preferencesSettingsManager, ILogger<PreferencesSetCommand> logger)
    : ICommandOptionsHandler<PreferencesSetCommandOptions>
{
    public async Task<int> HandleAsync(PreferencesSetCommandOptions options, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackPreferencesRun();

        var preferences = await preferencesSettingsManager.GetUserLeapPreferencesAsync(cancellationToken);
        logger.LogInformation("Setting preference for service `{Service}` to use `{Runner}` runner...", options.Service, options.Runner);
        preferences.Services[options.Service] = new Preference(options.Runner);
        await preferencesSettingsManager.SaveUserLeapPreferencesAsync(preferences, cancellationToken);

        logger.LogInformation("Successfully set preference");
        return 0;
    }
}