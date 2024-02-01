namespace Leap.Cli.Configuration;

internal interface IUserSettingsManager
{
    Task<UserSettings?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken);

    Task AddLeapYamlFilePathAsync(string leapYamlFilePath, CancellationToken cancellationToken);

    Task RemoveLeapYamlFilePathAsync(string leapYamlFilePath, CancellationToken cancellationToken);
}