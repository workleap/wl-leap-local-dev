using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed partial class AzuriteDependencyHandler : DependencyHandler<AzuriteDependency>
{
    private const string ServiceName = "azurite";
    private const string ContainerName = "leap-azurite";
    private const string DataVolumeName = "leap_azurite_data";

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public AzuriteDependencyHandler(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        IHttpClientFactory httpClientFactory,
        ILogger<AzuriteDependencyHandler> logger)
    {
        this._dockerCompose = dockerCompose;
        this._environmentVariables = environmentVariables;
        this._appSettingsJson = appSettingsJson;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    protected override Task BeforeStartAsync(AzuriteDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackAzuriteStart();
        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = "mcr.microsoft.com/azure-storage/azurite:latest",
            ContainerName = ContainerName,
            Command = new DockerComposeCommandYaml
            {
                "azurite",
                "--skipApiVersionCheck", // Don't throw if Azurite is more recent than the SDKs used to communicate with it
                "--loose", // Don't throw on invalid request headers or parameters
                "--location", "/data", // This is our persistent data volume

                // Bind each workload on a non-standard port to prevent conflicts with developers that already use Azurite with default ports
                "--blobHost", "0.0.0.0", "--blobPort", AzuriteConstants.BlobPort.ToString(),
                "--queueHost", "0.0.0.0", "--queuePort", AzuriteConstants.QueuePort.ToString(),
                "--tableHost", "0.0.0.0", "--tablePort", AzuriteConstants.TablePort.ToString(),

                // Enable HTTPS to support Azure SDKs with Azure.Identity (DefaultAzureCredential, etc.)
                // https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite#https-setup
                "--oauth", "basic",
                "--cert", $"/cert/{Constants.LeapCertificateCrtFileName}",
                "--key", $"/cert/{Constants.LeapCertificateKeyFileName}",
            },
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports =
            {
                new DockerComposePortMappingYaml(AzuriteConstants.BlobPort, AzuriteConstants.BlobPort),
                new DockerComposePortMappingYaml(AzuriteConstants.QueuePort, AzuriteConstants.QueuePort),
                new DockerComposePortMappingYaml(AzuriteConstants.TablePort, AzuriteConstants.TablePort),
            },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(DataVolumeName, "/data", DockerComposeConstants.Volume.ReadWrite),
                new DockerComposeVolumeMappingYaml(Constants.CertificatesDirectoryPath, "/cert", DockerComposeConstants.Volume.ReadOnly),
            },
            Deploy = new DockerComposeDeploymentYaml
            {
                Resources = new DockerComposeResourcesYaml
                {
                    Limits = new DockerComposeCpusAndMemoryYaml
                    {
                        Cpus = "0.5",
                        Memory = "500M",
                    },
                },
            },
        };

        dockerComposeYaml.Services[ServiceName] = service;
        dockerComposeYaml.Volumes[DataVolumeName] = null;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(new EnvironmentVariable[]
        {
            // Alternative for projects using a service URI instead of a connection string, which should be the main way to configure Azure Storage (with managed identity)
            // The value can be replaced in non-local environments with the actual URI of an Azure Storage account resource with token credentials
            // Can also be bound to Azure SDK clients using Microsoft.Extensions.Azure because this lib will look for the "serviceUri" parameter name
            new("Azure__Storage__Blob__ServiceUri", AzuriteConstants.HostBlobServiceUri, EnvironmentVariableScope.Host),
            new("Azure__Storage__Queue__ServiceUri", AzuriteConstants.HostQueueServiceUri, EnvironmentVariableScope.Host),
            new("Azure__Storage__Table__ServiceUri", AzuriteConstants.HostTableServiceUri, EnvironmentVariableScope.Host),

            new("Azure__Storage__Blob__ServiceUri",  AzuriteConstants.ContainerBlobServiceUri, EnvironmentVariableScope.Container),
            new("Azure__Storage__Queue__ServiceUri", AzuriteConstants.ContainerQueueServiceUri, EnvironmentVariableScope.Container),
            new("Azure__Storage__Table__ServiceUri", AzuriteConstants.ContainerTableServiceUri, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["Azure:Storage:Blob:ServiceUri"] = AzuriteConstants.HostBlobServiceUri;
        appsettings["Azure:Storage:Queue:ServiceUri"] = AzuriteConstants.HostQueueServiceUri;
        appsettings["Azure:Storage:Table:ServiceUri"] = AzuriteConstants.HostTableServiceUri;
    }

    protected override async Task AfterStartAsync(AzuriteDependency dependency, CancellationToken cancellationToken)
    {
        var httpClient = this._httpClientFactory.CreateClient(AzuriteConstants.HttpClientName);

        foreach (var container in dependency.Containers)
        {
            await this.CreateContainerAsync(httpClient, container, cancellationToken);
        }

        foreach (var table in dependency.Tables)
        {
            await this.CreateTableAsync(httpClient, table, cancellationToken);
        }

        foreach (var queue in dependency.Queues)
        {
            await this.CreateQueueAsync(httpClient, queue, cancellationToken);
        }

        this._logger.LogInformation("Azurite is ready");
    }

    private async Task CreateContainerAsync(HttpMessageInvoker httpClient, string container, CancellationToken cancellationToken)
    {
        this._logger.LogDebug("Creating blob storage container '{Container}'...", container);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-container?tabs=shared-key
        var createContainerUri = new UriBuilder(AzuriteConstants.HostBlobServiceUri);
        createContainerUri.Path += "/" + container;
        createContainerUri.Query += "&restype=container";

        using var request = new HttpRequestMessage(HttpMethod.Put, createContainerUri.Uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        this.WarnOnCreationFailure(response, "container", container);
    }

    private async Task CreateTableAsync(HttpMessageInvoker httpClient, string table, CancellationToken cancellationToken)
    {
        this._logger.LogDebug("Creating storage table '{Table}'...", table);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-table
        var createTableUri = new UriBuilder(AzuriteConstants.HostTableServiceUri);
        createTableUri.Path += "/Tables";

        var createTableBody = JsonSerializer.Serialize(new CreateTableRequestBody { TableName = table }, AzureStorageContext.Default.CreateTableRequestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, createTableUri.Uri);
        request.Headers.TryAddWithoutValidation("Accept", MediaTypeNames.Application.Json);
        request.Content = new StringContent(createTableBody, encoding: null, MediaTypeNames.Application.Json);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        this.WarnOnCreationFailure(response, "table", table);
    }

    private async Task CreateQueueAsync(HttpMessageInvoker httpClient, string queue, CancellationToken cancellationToken)
    {
        this._logger.LogDebug("Creating storage queue '{Queue}'...", queue);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-queue4
        var createQueueUri = new UriBuilder(AzuriteConstants.HostQueueServiceUri);
        createQueueUri.Path += "/" + queue;

        using var request = new HttpRequestMessage(HttpMethod.Put, createQueueUri.Uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        this.WarnOnCreationFailure(response, "queue", queue);
    }

    private void WarnOnCreationFailure(HttpResponseMessage response, string resourceType, string resourceName)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            this._logger.LogWarning("Creating the storage{ResourceType} '{ResourceName}' has failed", resourceType, resourceName);
        }
    }

    [JsonSerializable(typeof(CreateTableRequestBody))]
    private partial class AzureStorageContext : JsonSerializerContext
    {
    }

    private sealed class CreateTableRequestBody
    {
        [JsonPropertyName("TableName")]
        public string TableName { get; set; } = string.Empty;
    }
}