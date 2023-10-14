namespace Leap.Cli.Configuration;

internal static class ConfigurationConstants
{
    private const string LeapRootConfigDirectoryName = ".leap";
    private const string LeapGeneratedDirectoryName = "generated";

    public const string LeapYamlFileName = "leap.yaml";
    public const string SecondaryLeapYamlFileName = "leap.yml";

    private static readonly string UserProfileDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// The root directory path for Leap configuration: <c>~/.leap/</c>.
    /// </summary>
    public static readonly string RootDirectoryPath = Path.Combine(UserProfileDirectoryPath, LeapRootConfigDirectoryName);

    /// <summary>
    /// The directory that contains auto-generated Leap files: <c>~/.leap/generated/</c>.
    /// </summary>
    public static readonly string GeneratedDirectoryPath = Path.Combine(RootDirectoryPath, LeapGeneratedDirectoryName);
}