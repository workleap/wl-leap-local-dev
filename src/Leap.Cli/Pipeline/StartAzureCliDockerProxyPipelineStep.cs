using System.Globalization;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CliWrap;
using Leap.Cli.Aspire;
using Leap.Cli.Configuration;
using Leap.Cli.Model;
using Leap.Cli.Model.Traits;
using Leap.Cli.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

// Same concept as our Azure CLI credentials proxy for Docker containers,
// but applied everywhere to speed-up the acquisition of Azure CLI tokens.
// https://github.com/workleap/azure-cli-credentials-proxy
internal sealed class StartAzureCliDockerProxyPipelineStep(
    ICliWrap cliWrap,
    IAspireManager aspireManager,
    IConfigureEnvironmentVariables environmentVariables,
    LeapConfigManager leapConfigManager,
    ILogger<StartAzureCliDockerProxyPipelineStep> logger)
    : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (leapConfigManager.RemoteEnvironmentName is not null)
        {
            return;
        }

        var isAzureCliInstalled = await this.IsAzureCliInstalledAsync(cancellationToken);
        if (!isAzureCliInstalled)
        {
            logger.LogTrace("Azure CLI is not installed, skipping setup of Azure CLI proxy for Docker containers...");
            return;
        }

        logger.LogDebug("Checking if logged in to Azure CLI...");

        var isAzureCliLoggedIn = await this.IsAzureCliLoggedInAsync(cancellationToken);
        if (!isAzureCliLoggedIn)
        {
            throw new LeapException("Azure CLI is installed but not logged in. Please run 'az login' to login.");
        }

        var hasNoServiceToRun = state.Services.Count == 0;
        var hasDependenciesNeedingCliProxy = state.Dependencies.Any(x => x is IRequireAzCLIProxy);

        if (hasNoServiceToRun && !hasDependenciesNeedingCliProxy)
        {
            return;
        }

        this.AddAzureCliCredentialsProxyAspireResource();

        state.Dependencies.Add(new AzureCliDockerProxyDependency());
    }

    private async Task<bool> IsAzureCliInstalledAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new Command("az").WithArguments("version").WithValidation(CommandResultValidation.ZeroExitCode);
            await cliWrap.ExecuteBufferedAsync(command, cancellationToken);
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
            await cliWrap.ExecuteBufferedAsync(command, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void AddAzureCliCredentialsProxyAspireResource()
    {
        aspireManager.Builder.Services.TryAddEventingSubscriber<HostAzureCliDockerProxyInAspireLifecycleHook>();
        aspireManager.Builder.Services.TryAddSingleton(cliWrap);

        aspireManager.Builder.AddResource(new AzureCliDockerProxyResource())
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = Constants.LeapDependencyAspireResourceType,
                State = KnownResourceStates.Starting,
                CreationTimeStamp = DateTime.Now,
                Properties = [new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, "leap")]
            });

        environmentVariables.Configure(x =>
        {
            var proxyTokenEndpointUrl = $"http://127.0.0.1:{Constants.LeapAzureCliProxyPort}/token";
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

    private sealed class AzureCliDockerProxyResource() : Resource(Constants.LeapAzureCliProxyResourceName);

    private sealed class HostAzureCliDockerProxyInAspireLifecycleHook(ILogger<HostAzureCliDockerProxyInAspireLifecycleHook> logger, ICliWrap cliWrap, ResourceNotificationService notificationService, ResourceLoggerService loggerService)
        : IDistributedApplicationEventingSubscriber, IAsyncDisposable
    {
        private WebApplication? _app;
        private Task? _trackTask;

        public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
        {
            eventing.Subscribe<BeforeStartEvent>(this.BeforeStartAsync);
            return Task.CompletedTask;
        }

        private Task BeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
        {
            this._trackTask = this.TrackAzureCliProxyResource(@event.Model, cancellationToken);
            return Task.CompletedTask;
        }

        private async Task TrackAzureCliProxyResource(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            var proxyResource = appModel.Resources.OfType<AzureCliDockerProxyResource>().Single();
            var resourceLogger = loggerService.GetLogger(proxyResource);

            try
            {
                var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
                {
                    EnvironmentName = Environments.Production,
                });

                builder.WebHost.UseUrls($"http://+:{Constants.LeapAzureCliProxyPort}");

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
                // https://github.com/workleap/azure-cli-credentials-proxy/blob/1.0.1/Program.cs
                this._app.MapGet("/token", async (string resource, CancellationToken requestCancellationToken) =>
                {
                    return await this.GetAccessTokenAsync(resource, requestCancellationToken);
                });

                // Explain what this is when developers click on the resource URL in the Aspire dashboard
                this._app.MapGet("/", static async (HttpContext context, CancellationToken requestCancellationToken) =>
                {
                    const string htmlHelp = /*lang=html*/"""
                                                         This service enables containerized applications to access your Azure CLI developer credentials
                                                         when authenticating against Azure services using RBAC with your identity,
                                                         without requiring the installation of the Azure CLI in the container.
                                                         Learn more at <a href="https://github.com/workleap/azure-cli-credentials-proxy">https://github.com/workleap/azure-cli-credentials-proxy</a>.
                                                         """;

                    context.Response.ContentType = MediaTypeNames.Text.Html;
                    await context.Response.WriteAsync(htmlHelp, cancellationToken: requestCancellationToken);
                });

                await this._app.StartAsync(cancellationToken);

                await notificationService.PublishUpdateAsync(proxyResource, state => state with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.Now,
                    Urls = [new UrlSnapshot(Name: "http", Url: $"http://127.0.0.1:{Constants.LeapAzureCliProxyPort}", IsInternal: false)],
                });
            }
            catch (OperationCanceledException)
            {
                // swallowing OperationCanceledExceptions given that the application is shutting down at this point
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while starting Azure CLI credentials proxy for Docker");

                await notificationService.PublishUpdateAsync(proxyResource, state => state with
                {
                    State = KnownResourceStates.FailedToStart,
                    StopTimeStamp = DateTime.Now,
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

            if (this._trackTask != null)
            {
                try
                {
                    await this._trackTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when the application is shutting down.
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while tracking Docker Compose services.");
                }
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