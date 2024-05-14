using Microsoft.Extensions.Options;

namespace Leap.Cli.Platform;

internal sealed class FeatureManager(IOptions<LeapGlobalOptions> options) : IFeatureManager
{
    private readonly HashSet<string> _featureFlags = new HashSet<string>(options.Value.FeatureFlags, StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string featureName)
    {
        return this._featureFlags.Contains(featureName);
    }
}
