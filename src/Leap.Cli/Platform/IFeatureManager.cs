namespace Leap.Cli.Platform;

internal interface IFeatureManager
{
    bool IsEnabled(string featureName);
}