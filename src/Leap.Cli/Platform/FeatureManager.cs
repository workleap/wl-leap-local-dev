using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

internal sealed class FeatureManager : IFeatureManager
{
    private readonly HashSet<string> _featureFlags;

    public FeatureManager(string[] featureFlags)
    {
        this._featureFlags = new HashSet<string>(featureFlags, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEnabled(string featureName)
    {
        return this._featureFlags.Contains(featureName);
    }
}
