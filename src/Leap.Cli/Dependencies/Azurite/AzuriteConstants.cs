using Leap.Cli.Platform;

namespace Leap.Cli.Dependencies.Azurite;

internal static class AzuriteConstants
{
    public const string HttpClientName = "Azurite";

    // Default well-known Azure Storage emulator account name and key
    private const string AccountName = "devstoreaccount1";

    // Non-default ports for Azurite services to avoid conflicts with default ports which may be in use
    public const int BlobPort = 10050;
    public const int QueuePort = 10051;
    public const int TablePort = 10052;

    // Individual Azure Storage service URIs
    public static readonly string HostBlobServiceUri = $"https://127.0.0.1:{BlobPort}/{AccountName}";
    public static readonly string HostQueueServiceUri = $"https://127.0.0.1:{QueuePort}/{AccountName}";
    public static readonly string HostTableServiceUri = $"https://127.0.0.1:{TablePort}/{AccountName}";

    public static readonly string ContainerBlobServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostBlobServiceUri);
    public static readonly string ContainerQueueServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostQueueServiceUri);
    public static readonly string ContainerTableServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostTableServiceUri);
}