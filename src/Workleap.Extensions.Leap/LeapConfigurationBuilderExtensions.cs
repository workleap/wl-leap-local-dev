using Leap.Cli;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Configuration;
#pragma warning restore IDE0130

public static class LeapConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds Leap's local dev ~/.leap/generated/appsettings.json file only if it exists.
    /// Also, instruct Kestrel to use the local development certificate only if it exists as well.
    /// In cloud environments, it results in a no-op as the files are not present.
    /// </summary>
    public static IConfigurationBuilder AddLeap(this IConfigurationBuilder builder)
    {
        if (File.Exists(Constants.LocalCertificateCrtFilePath) && File.Exists(Constants.LocalCertificateKeyFilePath))
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kestrel:Certificates:Default:Path"] = Constants.LocalCertificateCrtFilePath,
                ["Kestrel:Certificates:Default:KeyPath"] = Constants.LocalCertificateKeyFilePath,
            });
        }

        builder.AddJsonFile(Constants.LeapAppSettingsFilePath, optional: true, reloadOnChange: true);

        return builder;
    }
}