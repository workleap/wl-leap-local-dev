using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpenTelemetry;

namespace Leap.Cli.Platform.Telemetry;

internal sealed class SetEndUserIdTagProcessor : BaseProcessor<Activity>
{
    private readonly Lazy<string> _lazyEndUserId;

    public SetEndUserIdTagProcessor()
    {
        this._lazyEndUserId = new Lazy<string>(ComputeAnonymizedEndUserId);
    }

    public override void OnEnd(Activity activity)
    {
        // This is an anonymized identifier that is unique to the user and the machine.
        activity.SetTag(TelemetryConstants.Attributes.EndUser.Id, this._lazyEndUserId.Value);
    }

    // This algorithm is similar to the one used by our ShareGate migration tool where a "machine code" represents a unique user and machine combination.
    // It's been used for more than 10 years and has proven to be reliable in our licensing system.
    // https://dev.azure.com/sharegate/ShareGate.Desktop/_git/ShareGate%20Desktop?path=%2FSource%2FCommon%2FSharegate.Common%2FLicensing%2FDefaultSystemIdentificationSource.cs&version=GT24.2.3&_a=contents
    private static string ComputeAnonymizedEndUserId()
    {
        var endUserIdMaterial = GenerateEndUserIdMaterial();
        var endUserIdMaterialBytes = Encoding.UTF8.GetBytes(endUserIdMaterial);
        var endUserIdMaterialHash = SHA256.HashData(endUserIdMaterialBytes);

        return Convert.ToHexString(endUserIdMaterialHash).ToLowerInvariant();
    }

    private static string GenerateEndUserIdMaterial()
    {
        var sb = new StringBuilder();

        sb.Append(Environment.MachineName);
        sb.Append('|');
        sb.Append(Environment.UserDomainName);
        sb.Append('|');
        sb.Append(Environment.UserName);

        return sb.ToString().ToUpperInvariant();
    }
}