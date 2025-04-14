using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed partial class AzuriteDependencyHandler(
    IConfigureDockerCompose dockerCompose,
    IConfigureEnvironmentVariables environmentVariables,
    IConfigureAppSettingsJson appSettingsJson,
    IHttpClientFactory httpClientFactory,
    IAspireManager aspire)
    : DependencyHandler<AzuriteDependency>
{
    private const string ServiceName = AzuriteDependencyYaml.YamlDiscriminator;
    private const string ContainerName = "leap-azurite";
    private const string DataVolumeName = "leap_azurite_data";

    protected override Task HandleAsync(AzuriteDependency dependency, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackAzuriteStart();
        ConfigureDockerCompose(dockerCompose.Configuration);
        environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(appSettingsJson.Configuration);

        aspire.Builder.AddDockerComposeResource(new DockerComposeResource(ServiceName, ContainerName)
        {
            ResourceType = Constants.LeapDependencyAspireResourceType,
            Urls = [AzuriteConstants.HostBlobServiceUri, AzuriteConstants.HostQueueServiceUri, AzuriteConstants.HostTableServiceUri]
        });

        aspire.Builder.Eventing.Subscribe<ResourceReadyEvent>(ServiceName, async (evt, ct) =>
        {
            var resourceLogger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(ServiceName);
            await this.OnAzuriteResourceReady(dependency, resourceLogger, ct);
        });

        return Task.CompletedTask;
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var service = new DockerComposeServiceYaml
        {
            Image = new DockerComposeImageName("mcr.microsoft.com/azure-storage/azurite:3.34.0"),
            ContainerName = ContainerName,
            Command =
            [
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
            ],
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
            Healthcheck = new DockerComposeHealthcheckYaml
            {
                // https://github.com/Azure/Azurite/issues/1666
                Test = ["CMD-SHELL", $"nc 127.0.0.1 {AzuriteConstants.BlobPort} -z && nc 127.0.0.1 {AzuriteConstants.QueuePort} -z && nc 127.0.0.1 {AzuriteConstants.TablePort} -z"],
                Interval = "1s",
                Retries = 30,
            }
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

    private async Task OnAzuriteResourceReady(AzuriteDependency dependency, ILogger resourceLogger, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(AzuriteConstants.HttpClientName);

        foreach (var container in dependency.Containers)
        {
            await CreateContainerAsync(httpClient, resourceLogger, container, cancellationToken);
        }

        foreach (var table in dependency.Tables)
        {
            await CreateTableAsync(httpClient, resourceLogger, table, cancellationToken);
        }

        foreach (var queue in dependency.Queues)
        {
            await CreateQueueAsync(httpClient, resourceLogger, queue, cancellationToken);
        }

        resourceLogger.LogInformation("Azurite is ready");
    }

    private static async Task CreateContainerAsync(HttpMessageInvoker httpClient, ILogger resourceLogger, string container, CancellationToken cancellationToken)
    {
        resourceLogger.LogDebug("Creating blob storage container '{Container}'...", container);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-container?tabs=shared-key
        var createContainerUri = new UriBuilder(AzuriteConstants.HostBlobServiceUri);
        createContainerUri.Path += "/" + container;
        createContainerUri.Query += "&restype=container";

        using var request = new HttpRequestMessage(HttpMethod.Put, createContainerUri.Uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        WarnOnCreationFailure(response, resourceLogger, "container", container);
    }

    private static async Task CreateTableAsync(HttpMessageInvoker httpClient, ILogger resourceLogger, string table, CancellationToken cancellationToken)
    {
        resourceLogger.LogDebug("Creating storage table '{Table}'...", table);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-table
        var createTableUri = new UriBuilder(AzuriteConstants.HostTableServiceUri);
        createTableUri.Path += "/Tables";

        var createTableBody = JsonSerializer.Serialize(new CreateTableRequestBody { TableName = table }, AzureStorageContext.Default.CreateTableRequestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, createTableUri.Uri);
        request.Headers.TryAddWithoutValidation("Accept", MediaTypeNames.Application.Json);
        request.Content = new StringContent(createTableBody, encoding: null, MediaTypeNames.Application.Json);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        WarnOnCreationFailure(response, resourceLogger, "table", table);
    }

    private static async Task CreateQueueAsync(HttpMessageInvoker httpClient, ILogger resourceLogger, string queue, CancellationToken cancellationToken)
    {
        resourceLogger.LogDebug("Creating storage queue '{Queue}'...", queue);

        // https://learn.microsoft.com/en-us/rest/api/storageservices/create-queue4
        var createQueueUri = new UriBuilder(AzuriteConstants.HostQueueServiceUri);
        createQueueUri.Path += "/" + queue;

        using var request = new HttpRequestMessage(HttpMethod.Put, createQueueUri.Uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        WarnOnCreationFailure(response, resourceLogger, "queue", queue);
    }

    private static void WarnOnCreationFailure(HttpResponseMessage response, ILogger resourceLogger, string resourceType, string resourceName)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            resourceLogger.LogWarning("Creating the storage{ResourceType} '{ResourceName}' has failed", resourceType, resourceName);
        }
    }

    [JsonSerializable(typeof(CreateTableRequestBody))]
    private partial class AzureStorageContext : JsonSerializerContext;

    private sealed class CreateTableRequestBody
    {
        [JsonPropertyName("TableName")]
        public string TableName { get; set; } = string.Empty;
    }
}