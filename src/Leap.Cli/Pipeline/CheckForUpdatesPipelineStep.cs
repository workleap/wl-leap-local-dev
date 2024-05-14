using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Leap.Cli.Pipeline;

internal sealed class CheckForUpdatesPipelineStep(
    IHttpClientFactory httpClientFactory,
    IOptions<LeapGlobalOptions> options,
    IPlatformHelper platformHelper,
    ILogger<CheckForUpdatesPipelineStep> logger) : IPipelineStep
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(Constants.AzureDevOps.HttpClientName);

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (options.Value.SkipVersionCheck)
        {
            return;
        }

        try
        {
            logger.LogInformation("Checking for updates...");

            // https://learn.microsoft.com/en-us/rest/api/azure/devops/artifacts/artifact-details/get-package?view=azure-devops-rest-7.0
            var urlBuilder = new StringBuilder()
                .AppendFormat(
                    "https://feeds.dev.azure.com/{0}/_apis/packaging/feeds/{1}/packages/{2}",
                    Constants.AzureDevOps.GSoftOrganizationName, Constants.AzureDevOps.GSoftFeedName, Constants.AzureDevOps.LeapNuGetPackageId)
                .Append("?includeAllVersions=false") // When false, only the latest version is returned according to other query parameters
                .Append("&includeDeleted=false") // Ignore deleted versions
                .Append("&isListed=true") // Ignore unlisted versions
                .Append("&isRelease=true") // Ignore preview versions (x.y.z-preview.n, etc.)
                .Append("&api-version=7.0"); // Latest stable at this moment

            var package = await this._httpClient.GetFromJsonAsync<AdoPackage>(urlBuilder.ToString(), cancellationToken);

            var latestVersion = package?.Versions?.Select(v => v.Version).FirstOrDefault()
                ?? throw new LeapException("Azure DevOps did not return any package versions for Leap CLI.");

            var currentVersion = platformHelper.CurrentApplicationVersion;

            var latestNuGetVersion = new NuGetVersion(latestVersion);
            var currentNuGetVersion = new NuGetVersion(currentVersion);

            // The NuGet versioning package implements proper NuGet version comparison logic
            if (latestNuGetVersion > currentNuGetVersion)
            {
                TelemetryMeters.TrackOutdatedLeap();

                var updateInstructions = $"""
                   ===============================================================
                   Update available: {currentVersion} -> {latestVersion}
                   Run: dotnet tool update {Constants.AzureDevOps.LeapNuGetPackageName} --global --interactive --add-source "{Constants.AzureDevOps.GSoftFeedUrl}" --verbosity minimal --no-cache
                   ==============================================================
                   """;
                logger.LogWarning("{UpdateInstructions}", updateInstructions.ReplaceLineEndings());

                var acknowledgmentDelay = TimeSpan.FromSeconds(3);
                await Task.Delay(acknowledgmentDelay, cancellationToken);
            }
            else
            {
                logger.LogInformation("Leap is up-to-date.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LeapException ex)
        {
            throw new LeapException($"{ex.Message.TrimEnd('.')}. Use {LeapGlobalOptions.SkipVersionCheckOptionName} to disable version checks.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"An unexpected error occurred while checking for updates. Use {LeapGlobalOptions.SkipVersionCheckOptionName} to disable version checks.", ex);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private sealed class AdoPackage
    {
        [JsonPropertyName("versions")]
        public AdoPackageVersion[]? Versions { get; set; }
    }

    private sealed class AdoPackageVersion
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}