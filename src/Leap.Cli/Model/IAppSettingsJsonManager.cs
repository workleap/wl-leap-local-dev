namespace Leap.Cli.Model;

internal interface IAppSettingsJsonManager : IConfigureAppSettingsJson
{
    Task WriteUpdatedAppSettingsJson(CancellationToken cancellationToken);
}