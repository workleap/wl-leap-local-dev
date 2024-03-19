namespace Leap.Cli;

internal static class Constants
{
    private const string LeapRootConfigDirectoryName = ".leap";
    private const string LeapGeneratedDirectoryName = "generated";
    private const string LeapDockerComposeDirectoryName = "docker-compose";
    private const string LeapCertificatesDirectoryName = "certificates";

    private const string EventGridSettingsFileName = "eventgridsettings.json";

    public const string LeapYamlFileName = "leap.yaml";
    public const string SecondaryLeapYamlFileName = "leap.yml";

    private const string LeapCertificateCrtFileName = "workleap-dev-certificate.crt";
    private const string LeapCertificateKeyFileName = "workleap-dev-certificate.key";

    // ".localhost" is a top-level domain (TLD) reserved by the Internet Engineering Task Force (IETF)
    // that is free to use localhost names as they would any other, without the risk of someone else owning it (like .com).
    // https://www.iana.org/assignments/special-use-domain-names/special-use-domain-names.xhtml
    // We didn't use ".local" because of the mDNS (Multicast DNS) protocol, which may cause issues according to this thread
    // https://www.reddit.com/r/sysadmin/comments/gdeggi/
    public static readonly string[] SupportedLocalDevelopmentCertificateDomainNames =
    [
        "localhost", "127.0.0.1", "::1", // localhost
        "host.docker.internal", "host.containers.internal", // Docker and Podman
        "*.officevibe.localhost", "*.officevibe-dev.localhost", // Officevibe
        "*.sharegate.localhost", "*.sharegate-dev.localhost", // ShareGate
        "*.workleap.localhost", "*.workleap-dev.localhost" // Workleap
    ];

    // "1347" means "leap" in leetspeak (https://en.wikipedia.org/wiki/Leet)
    public const int LeapReverseProxyPort = 1347;

    private static readonly string UserProfileDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The root directory path for Leap configuration</summary>
    public static readonly string RootDirectoryPath = Path.Combine(UserProfileDirectoryPath, LeapRootConfigDirectoryName);

    /// <summary>User-managed event grid configuration file path</summary>
    public static readonly string LeapEventGridSettingsFilePath = Path.Combine(RootDirectoryPath, EventGridSettingsFileName);

    /// <summary>The directory that contains auto-generated Leap files</summary>
    public static readonly string GeneratedDirectoryPath = Path.Combine(RootDirectoryPath, LeapGeneratedDirectoryName);

    /// <summary>The directory that contains the generated Docker Compose files</summary>
    public static readonly string DockerComposeDirectoryPath = Path.Combine(GeneratedDirectoryPath, LeapDockerComposeDirectoryName);

    /// <summary>The directory that contains the generated local development certificate files</summary>
    public static readonly string CertificatesDirectoryPath = Path.Combine(GeneratedDirectoryPath, LeapCertificatesDirectoryName);

    /// <summary>The path of the generated local development public certificate path</summary>
    public static readonly string LocalCertificateCrtFilePath = Path.Combine(CertificatesDirectoryPath, LeapCertificateCrtFileName);

    /// <summary>The path of the generated local development certificate private key path</summary>
    public static readonly string LocalCertificateKeyFilePath = Path.Combine(CertificatesDirectoryPath, LeapCertificateKeyFileName);
}
