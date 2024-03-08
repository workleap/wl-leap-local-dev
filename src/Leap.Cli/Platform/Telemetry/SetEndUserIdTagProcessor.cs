using System.Diagnostics;
using OpenTelemetry;

namespace Leap.Cli.Platform.Telemetry;

internal sealed class SetEndUserIdTagProcessor : BaseProcessor<Activity>
{
    private readonly Lazy<string> _lazyEndUserId;

    public SetEndUserIdTagProcessor()
    {
        this._lazyEndUserId = new Lazy<string>(TelemetryAnonymizer.ComputeAnonymizedEndUserId);
    }

    public override void OnEnd(Activity activity)
    {
        // This is an anonymized identifier that is unique to the user and the machine.
        activity.SetTag(TelemetryConstants.Attributes.EndUser.Id, this._lazyEndUserId.Value);
    }
}