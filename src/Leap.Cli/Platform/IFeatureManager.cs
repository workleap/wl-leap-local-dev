namespace Leap.Cli.Platform;

public interface IFeatureManager
{
    bool IsEnabled(string featureName);
}
