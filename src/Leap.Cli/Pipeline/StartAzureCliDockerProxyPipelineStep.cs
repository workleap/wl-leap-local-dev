using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class StartAzureCliDockerProxyPipelineStep : IPipelineStep
{
    private readonly ICliWrap _cliWrap;
    private readonly IPortManager _portManager;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly ILogger _logger;

    private WebApplication? _app;

    public StartAzureCliDockerProxyPipelineStep(
        ICliWrap cliWrap,
        IPortManager portManager,
        IConfigureEnvironmentVariables environmentVariables,
        ILogger<StartAzureCliDockerProxyPipelineStep> logger)
    {
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
            return;
        }

        this._logger.LogDebug("Checking if logged in to Azure CLI...");

        var isAzureCliLoggedIn = await this.IsAzureCliLoggedInAsync(cancellationToken);
        if (!isAzureCliLoggedIn)
        {
            throw new LeapException("Azure CLI is installed but not logged in. Please run `az login` to login.");
        }

        var hasAtLeastOneDockerBinding = state.Services.Values.Any(x => x.ActiveBinding is DockerBinding);
        if (!hasAtLeastOneDockerBinding)
        {
            return;
        }

        // Docker containers cannot access the host's Azure CLI credentials, because they don't ship with the Azure CLI.
        // It's the same concept that we used here: https://github.com/gsoft-inc/azure-cli-credentials-proxy
        await this.RunAzureCliCredentialsProxyAsync(cancellationToken);
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

    private async Task RunAzureCliCredentialsProxyAsync(CancellationToken cancellationToken)
    {
        var port = this._portManager.GetRandomAvailablePort(cancellationToken);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });

        builder.WebHost.UseUrls("http://+:" + port);

        // Suppress Ctrl+C, SIGINT, and SIGTERM signals because already handled by System.CommandLine
        // through the cancellation token that is passed to the pipeline step.
        builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new SimpleColoredConsoleLoggerProvider());

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Warning",
        });

        this._app = builder.Build();

        // See our Azure CLI credentials proxy which is distributed as a Docker image:
        // https://github.com/gsoft-inc/azure-cli-credentials-proxy/blob/1.0.1/Program.cs
        this._app.MapGet("/token", async (string resource, CancellationToken requestCancellationToken) =>
        {
            return await this.GetAccessTokenAsync(resource, requestCancellationToken);
        });

        this._logger.LogInformation("Starting Azure CLI credentials proxy for Docker containers on port {Port}...", port);

        await this._app.StartAsync(cancellationToken);

        this._environmentVariables.Configure(x =>
        {
            x.Add(new EnvironmentVariable("IDENTITY_ENDPOINT", "http://host.docker.internal:" + port, EnvironmentVariableScope.Container));
            x.Add(new EnvironmentVariable("IMDS_ENDPOINT", "dummy_required_value" + port, EnvironmentVariableScope.Container));
        });
    }

    private async Task<AccessTokenDto> GetAccessTokenAsync(string resource, CancellationToken cancellationToken)
    {
        // See: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.10.4/sdk/identity/Azure.Identity/src/Credentials/AzureCliCredential.cs#L212
        var args = new[] { "account", "get-access-token", "--output", "json", "--resource", resource };
        var command = new Command("az").WithArguments(args).WithValidation(CommandResultValidation.ZeroExitCode);
        var result = await this._cliWrap.ExecuteBufferedAsync(command, cancellationToken);

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

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._app != null)
        {
            await this._app.DisposeAsync();
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