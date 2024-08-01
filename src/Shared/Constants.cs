namespace Leap.Cli;

internal static class Constants
{
    private const string LeapRootConfigDirectoryName = ".leap";
    private const string LeapGeneratedDirectoryName = "generated";
    private const string LeapDockerComposeDirectoryName = "docker-compose";
    private const string LeapCertificatesDirectoryName = "certificates";
    private const string LeapNuGetPackagesDirectoryName = "nuget-packages";

    private const string AppSettingsFileName = "appsettings.json";
    private const string EventGridSettingsFileName = "eventgridsettings.json";
    public const string FusionAuthKickstartFileName = "kickstart-local.json";

    public const string LeapYamlFileName = "leap.yaml";
    public const string SecondaryLeapYamlFileName = "leap.yml";

    private const string LeapCertificateCrtFileName = "workleap-dev-certificate.crt";
    private const string LeapCertificateKeyFileName = "workleap-dev-certificate.key";

    public const string LeapDependencyAspireResourceType = "Leap dependency";

    // ".localhost" is a top-level domain (TLD) reserved by the Internet Engineering Task Force (IETF)
    // that is free to use localhost names as they would any other, without the risk of someone else owning it (like .com).
    // https://www.iana.org/assignments/special-use-domain-names/special-use-domain-names.xhtml
    // We didn't use ".local" because of the mDNS (Multicast DNS) protocol, which may cause issues according to this thread
    // https://www.reddit.com/r/sysadmin/comments/gdeggi/
    public static readonly string[] SupportedWildcardLocalhostDomainNames =
    [
        "*.officevibe.localhost",
        "*.sharegate.localhost",
        "*.workleap.localhost",

        // The following domain names go against our recommendation to use "*.localhost": https://gsoftdev.atlassian.net/wiki/x/nAHL9w.
        // We decided to allow them anyway to increase adoption in Officevibe and Workleap platform, as they are currently used in multiple places
        // in their codebase and cannot easily be migrated to "*.localhost" without breaking their hybrid cloud cookie-based authentication.
        "*.officevibe.com",
        "*.workleap.com",
        "*.officevibe-dev.com",
        "*.workleap-dev.com",
        "*.workleap-local.com",

        // ShareGate recently adopted a centralized cookie like Officevibe and Workleap, so we need to allow these domains as well
        // for them to do hybrid local/cloud cookie-based authentication.
        "*.sharegate-dev.com",
    ];

    public static readonly string[] MkcertSupportedDomainNames =
    [
        "localhost", "127.0.0.1", "::1", // localhost
        "host.docker.internal", "host.containers.internal", // Docker and Podman
        .. SupportedWildcardLocalhostDomainNames,
    ];

    // "1347" means "leap" in leetspeak (https://en.wikipedia.org/wiki/Leet)
    public const int LeapReverseProxyPort = 1347;

    private static readonly string UserProfileDirectoryPath = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>The root directory path for Leap configuration</summary>
    public static readonly string RootDirectoryPath = Path.Combine(UserProfileDirectoryPath, LeapRootConfigDirectoryName);

    /// <summary>User-managed event grid configuration file path</summary>
    public static readonly string LeapEventGridSettingsFilePath = Path.Combine(RootDirectoryPath, EventGridSettingsFileName);

    /// <summary>The directory that contains auto-generated Leap files</summary>
    public static readonly string GeneratedDirectoryPath = Path.Combine(RootDirectoryPath, LeapGeneratedDirectoryName);

    /// <summary>Leap generated FusionAuth kickstart file path</summary>
    public static readonly string FusionAuthKickstartFilePath = Path.Combine(GeneratedDirectoryPath, FusionAuthKickstartFileName);

    /// <summary>Leap generated appsettings.json file path</summary>
    public static readonly string LeapAppSettingsFilePath = Path.Combine(GeneratedDirectoryPath, AppSettingsFileName);

    /// <summary>The directory that contains the generated Docker Compose files</summary>
    public static readonly string DockerComposeDirectoryPath = Path.Combine(GeneratedDirectoryPath, LeapDockerComposeDirectoryName);

    /// <summary>The directory that contains the generated local development certificate files</summary>
    public static readonly string CertificatesDirectoryPath = Path.Combine(GeneratedDirectoryPath, LeapCertificatesDirectoryName);

    /// <summary>The path of the generated local development public certificate path</summary>
    public static readonly string LocalCertificateCrtFilePath = Path.Combine(CertificatesDirectoryPath, LeapCertificateCrtFileName);

    /// <summary>The path of the generated local development certificate private key path</summary>
    public static readonly string LocalCertificateKeyFilePath = Path.Combine(CertificatesDirectoryPath, LeapCertificateKeyFileName);

    /// <summary>The directory that contains NuGet packages that we download at runtime</summary>
    public static readonly string NuGetPackagesDirectoryPath = Path.Combine(GeneratedDirectoryPath, LeapNuGetPackagesDirectoryName);

    /// <summary>
    /// Constants for MSAL (Microsoft Authentication Library).
    /// </summary>
    public static class Msal
    {
        /// <summary>ID of our Leap Azure app registration.</summary>
        public const string ClientId = "261e4e88-1750-4008-9f95-d638b40d60d4";

        /// <summary>Workleap's Azure tenant ID available at https://portal.azure.com/</summary>
        public const string WorkleapTenantId = "eb39acb7-fae3-4bc3-974c-b765aa1d6355";

        /// <summary>
        /// Constants used to configure the MSAL token cache on Unix systems. See:
        /// https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/wiki/Cross-platform-Token-Cache
        /// </summary>
        public static class Cache
        {
            // Values inspired from the MSAL integration in Semantic Kernel
            // https://github.com/microsoft/semantic-kernel/blob/dotnet-1.11.1/dotnet/src/Plugins/Plugins.MsGraph/Connectors/CredentialManagers/LocalUserMSALCredentialManager.cs
            public const string CacheFileName = "leap.msalcache.bin";
            public static readonly string CacheDirectoryPath = GeneratedDirectoryPath;

            public const string MacKeyChainServiceName = "com.workleap.leap.tokencache.service";
            public const string MacKeyChainAccountName = "com.workleap.leap.tokencache.account";

            public const string LinuxKeyRingSchema = "com.workleap.leap.tokencache";
            public const string LinuxKeyRingCollection = "default";
            public const string LinuxKeyRingLabel = "MSAL token cache for Leap.";
            public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1"); // Desn't need to change
            public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("Product", "Leap");
        }
    }

    public static class AzureDevOps
    {
        /// <summary>
        /// Well-known Azure DevOps delegated scope (constant) used to access Azure DevOps APIs.
        /// https://learn.microsoft.com/en-us/rest/api/azure/devops/tokens/?view=azure-devops-rest-7.0
        /// </summary>
        public const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/user_impersonation";

        /// <summary>The ADO organization name where Leap is published.</summary>
        public const string GSoftOrganizationName = "gsoft";

        /// <summary>The name of the ADO artifacts feed where Leap is published.</summary>
        public const string GSoftFeedName = "gsoft";

        /// <summary>The URL of the Azure DevOps feed where Leap is published.</summary>
        public const string GSoftFeedUrl = "https://pkgs.dev.azure.com/gsoft/_packaging/gsoft/nuget/v3/index.json";

        /// <summary>The ID of the Leap CLI NuGet package in Azure DevOps feed.</summary>
        public const string LeapNuGetPackageId = "7e26c0cd-3179-49c3-a3a7-e92df061eee5";

        /// <summary>The name of the Leap CLI NuGet package.</summary>
        public const string LeapNuGetPackageName = "Workleap.Leap";

        /// <summary>Name of the HttpClient used to interact with Azure DevOps feeds.</summary>
        public const string HttpClientName = "AzureDevOps";
    }
}
