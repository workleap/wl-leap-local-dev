using Leap.Cli.Platform;

namespace Leap.Cli.Dependencies.Azurite;

internal static class AzuriteConstants
{
    public const string HttpClientName = "Azurite";

    // Default well-known Azure Storage emulator account name and key
    public const string AccountName = "devstoreaccount1";
    public const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    // 10-year valid SAS generated from the development credentials above, targeting all services, resource types, and permissions
    public const string SharedAccessSignature = "sv=2023-11-03&ss=bfqt&srt=sco&spr=https,http&se=2034-01-20T01%3A50%3A44Z&sp=rwdxylacuptfi&sig=t1ENsexdjhwa9VUAzlzaDqRcP9yk6qUZ6P%2Fqw%2FjVYjQ%3D";

    // Non-default ports for Azurite services to avoid conflicts with default ports which may be in use
    public const int BlobPort = 10050;
    public const int QueuePort = 10051;
    public const int TablePort = 10052;

    // Full connection string for all Azure Storage services
    public static readonly string HostConnectionString = $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};BlobEndpoint=http://127.0.0.1:{BlobPort}/{AccountName};QueueEndpoint=http://127.0.0.1:{QueuePort}/{AccountName};TableEndpoint=http://127.0.0.1:{TablePort}/{AccountName};";
    public static readonly string ContainerConnectionString = HostNameResolver.ReplaceLocalhostWithContainerHost(HostConnectionString);

    // Individual Azure Storage service URIs with SAS
    public static readonly string HostBlobServiceUri = $"http://127.0.0.1:{BlobPort}/{AccountName}?{SharedAccessSignature}";
    public static readonly string HostQueueServiceUri = $"http://127.0.0.1:{QueuePort}/{AccountName}?{SharedAccessSignature}";
    public static readonly string HostTableServiceUri = $"http://127.0.0.1:{TablePort}/{AccountName}?{SharedAccessSignature}";

    public static readonly string ContainerBlobServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostBlobServiceUri);
    public static readonly string ContainerQueueServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostQueueServiceUri);
    public static readonly string ContainerTableServiceUri = HostNameResolver.ReplaceLocalhostWithContainerHost(HostTableServiceUri);
}