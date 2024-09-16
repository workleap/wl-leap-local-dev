using System.Globalization;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.Lifecycle;
using CliWrap;
using Leap.Cli.Aspire;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class StartAzureCliDockerProxyPipelineStep : IPipelineStep
{
    private readonly ICliWrap _cliWrap;
    private readonly IPortManager _portManager;
    private readonly IAspireManager _aspireManager;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly ILogger _logger;

    public StartAzureCliDockerProxyPipelineStep(
        ICliWrap cliWrap,
        IPortManager portManager,
        IAspireManager aspireManager,
        IConfigureEnvironmentVariables environmentVariables,
        ILogger<StartAzureCliDockerProxyPipelineStep> logger)
    {
        this._aspireManager = aspireManager;
        this._cliWrap = cliWrap;
        this._portManager = portManager;
        this._environmentVariables = environmentVariables;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var isAzureCliInstalled = await this.IsAzureCliInstalledAsync(cancellationToken);
        if (!isAzureCliInstalled)
        {
            this._logger.LogTrace("Azure CLI is not installed, skipping setup of Azure CLI proxy for Docker containers...");
            return;
        }

        this._logger.LogDebug("Checking if logged in to Azure CLI...");

        var isAzureCliLoggedIn = await this.IsAzureCliLoggedInAsync(cancellationToken);
        if (!isAzureCliLoggedIn)
        {
            throw new LeapException("Azure CLI is installed but not logged in. Please run `az login` to login.");
        }

        // Docker containers cannot access the host's Azure CLI credentials, because they don't ship with the Azure CLI.
        // It's the same concept that we used here: https://github.com/gsoft-inc/azure-cli-credentials-proxy
        this.RunAzureCliCredentialsProxyInAspireAsync(cancellationToken);
    }

    private async Task<bool> IsAzureCliInstalledAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new Command("az").WithArguments("version").WithValidation(CommandResultValidation.ZeroExitCode);
            await this._cliWrap.ExecuteBufferedAsync(command, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> IsAzureCliLoggedInAsync(CancellationToken cancellationToken)
    {
        try
        {
            var args = new[] { "account", "show", "--output", "json" };
            var command = new Command("az").WithArguments(args).WithValidation(CommandResultValidation.ZeroExitCode);
            await this._cliWrap.ExecuteBufferedAsync(command, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void RunAzureCliCredentialsProxyInAspireAsync(CancellationToken cancellationToken)
    {
        var proxyPort = this._portManager.GetRandomAvailablePort(cancellationToken);

        this._aspireManager.Builder.Services.TryAddLifecycleHook<HostAzureCliDockerProxyInAspireLifecycleHook>();
        this._aspireManager.Builder.Services.TryAddSingleton(this._cliWrap);

        this._aspireManager.Builder.AddResource(new AzureCliDockerProxyResource(proxyPort))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = Constants.LeapDependencyAspireResourceType,
                State = "Starting",
                Properties = [new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, "leap")]
            })
            .ExcludeFromManifest();

        this._environmentVariables.Configure(x =>
        {
            var proxyTokenEndpointUrl = $"http://127.0.0.1:{proxyPort}/token";
            const string dummyIdmsEndpoint = "dummy_required_value";

            x.Add(new EnvironmentVariable("IDENTITY_ENDPOINT", proxyTokenEndpointUrl, EnvironmentVariableScope.Host));
            x.Add(new EnvironmentVariable("IMDS_ENDPOINT", dummyIdmsEndpoint, EnvironmentVariableScope.Host));

            x.Add(new EnvironmentVariable("IDENTITY_ENDPOINT", HostNameResolver.ReplaceLocalhostWithContainerHost(proxyTokenEndpointUrl), EnvironmentVariableScope.Container));
            x.Add(new EnvironmentVariable("IMDS_ENDPOINT", dummyIdmsEndpoint, EnvironmentVariableScope.Container));
        });
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private sealed class AzureCliDockerProxyResource(int port) : Resource("azure-cli-credentials-proxy")
    {
        public int Port { get; } = port;
    }

    private sealed class HostAzureCliDockerProxyInAspireLifecycleHook(ICliWrap cliWrap, ResourceNotificationService notificationService, ResourceLoggerService loggerService)
        : IDistributedApplicationLifecycleHook, IAsyncDisposable
    {
        private WebApplication? _app;

        public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            var proxyResource = appModel.Resources.OfType<AzureCliDockerProxyResource>().Single();
            var resourceLogger = loggerService.GetLogger(proxyResource);

            try
            {
                var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
                {
                    EnvironmentName = Environments.Production,
                });

                builder.WebHost.UseUrls("http://+:" + proxyResource.Port);

                // Suppress Ctrl+C, SIGINT, and SIGTERM signals because already handled by System.CommandLine
                // through the cancellation token that is passed to the pipeline step.
                builder.IgnoreConsoleTerminationSignals();

                builder.Logging.ClearProviders();
                builder.Logging.AddResourceLogger(resourceLogger);

                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:LogLevel:Default"] = "Information",
                });

                this._app = builder.Build();

                // See our Azure CLI credentials proxy which is distributed as a Docker image:
                // https://github.com/gsoft-inc/azure-cli-credentials-proxy/blob/1.0.1/Program.cs
                this._app.MapGet("/token", async (string resource, CancellationToken requestCancellationToken) =>
                {
                    return await this.GetAccessTokenAsync(resource, requestCancellationToken);
                });

                // Explain what this is when developers click on the resource URL in the Aspire dashboard
                this._app.MapGet("/", static async (HttpContext context, CancellationToken requestCancellationToken) =>
                {
                    const string htmlHelp = """
                                            This service enables containerized applications to access your Azure CLI developer credentials
                                            when authenticating against Azure services using RBAC with your identity,
                                            without requiring the installation of the Azure CLI in the container.
                                            Learn more at <a href="https://github.com/gsoft-inc/azure-cli-credentials-proxy">https://github.com/gsoft-inc/azure-cli-credentials-proxy</a>.
                                            """;

                    context.Response.ContentType = MediaTypeNames.Text.Html;
                    await context.Response.WriteAsync(htmlHelp, cancellationToken: requestCancellationToken);
                });

                await this._app.StartAsync(cancellationToken);

                await notificationService.PublishUpdateAsync(proxyResource, state => state with
                {
                    State = "Running",
                    Urls = [new UrlSnapshot(Name: "http", Url: "http://127.0.0.1:" + proxyResource.Port, IsInternal: false)],
                });
            }
            catch (Exception ex)
            {
                resourceLogger.LogError(ex, "An error occured while starting Azure CLI credentials proxy for Docker");

                await notificationService.PublishUpdateAsync(proxyResource, state => state with
                {
                    State = "Finished"
                });
            }
        }

        private async Task<AccessTokenDto> GetAccessTokenAsync(string resource, CancellationToken cancellationToken)
        {
            // See: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.10.4/sdk/identity/Azure.Identity/src/Credentials/AzureCliCredential.cs#L212
            var args = new[] { "account", "get-access-token", "--output", "json", "--resource", resource };
            var command = new Command("az").WithArguments(args).WithValidation(CommandResultValidation.ZeroExitCode);
            var result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);

            // Deserialization logic from:
            // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.10.4/sdk/identity/Azure.Identity/src/Credentials/AzureCliCredential.cs#L230-L241
            using var document = JsonDocument.Parse(result.StandardOutput);

            var root = document.RootElement;
            var accessToken = root.GetProperty("accessToken").GetString()!;
            var expiresOn = root.TryGetProperty("expiresIn", out var expiresIn)
                ? DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresIn.GetInt64())
                : DateTimeOffset.ParseExact(root.GetProperty("expiresOn").GetString()!, "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeLocal);

            return new AccessTokenDto
            {
                AccessToken = accessToken,

                // Seems like some Azure CLI SDKs for other languages such as go only support seconds instead of ISO 8601
                // Based on a fork of our Workleap Azure CLI credentials proxy:
                // https://github.com/SnowSoftwareGlobal/azure-cli-credentials-proxy/commit/3d88359b4a7922e98c242807258de9f98b819d73
                //
                // In any case, we know it works with the Azure CLI .NET SDK:
                // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.10.4/sdk/identity/Azure.Identity/src/ManagedIdentitySource.cs#L149-L164
                ExpiresOn = expiresOn.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (this._app != null)
            {
                await this._app.DisposeAsync();
            }
        }
    }

    private sealed class AccessTokenDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_on")]
        public string ExpiresOn { get; init; } = string.Empty;
    }
}