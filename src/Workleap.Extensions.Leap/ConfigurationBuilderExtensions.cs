using Leap.Cli;
using Microsoft.Extensions.Configuration;

namespace Workleap.Extensions.Leap;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddLeapConfiguration(this IConfigurationBuilder builder)
    {
        builder.AddJsonFile(Constants.LeapAppSettingsFilePath, optional: true, reloadOnChange: true);

        return builder;
    }
}