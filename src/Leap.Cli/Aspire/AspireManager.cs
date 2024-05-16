using System.Reflection;
using System.Runtime.InteropServices;
using Aspire.Hosting.Lifecycle;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Aspire;

internal sealed class AspireManager : IAspireManager
{
    // https://github.com/dotnet/aspire/blob/v8.0.0-preview.3.24105.21/src/Aspire.Dashboard/DashboardWebApplication.cs#L18-L21
    public const string AspireDashboardOtlpUrlDefaultValue = "https://localhost:18889";
    public const string AspireDashboardUrlDefaultValue = "https://localhost:18888";
    public const string AspireResourceServiceEndpointUrl = "https://localhost:18887";

    private readonly ILogger _logger;
    private readonly INuGetPackageDownloader _nuGetPackageDownloader;
    private readonly IPlatformHelper _platformHelper;
    private readonly IOptions<LeapGlobalOptions> _leapGlobalOptions;

    private Task<string> _downloadAspireOrchestrationPackageTask = Task.FromResult(string.Empty);
    private Task<string> _downloadAspireDashboardPackageTask = Task.FromResult(string.Empty);

    public AspireManager(
        ILogger<AspireManager> logger,
        INuGetPackageDownloader nuGetPackageDownloader,
        IPlatformHelper platformHelper,
        IOptions<LeapGlobalOptions> leapGlobalOptions)
    {
        this._logger = logger;
        this._nuGetPackageDownloader = nuGetPackageDownloader;
        this._platformHelper = platformHelper;
        this._leapGlobalOptions = leapGlobalOptions;

        this.Builder = DistributedApplication.CreateBuilder();
    }

    public IDistributedApplicationBuilder Builder { get; }

    public void BeginAspireWorkloadDownloadTask(CancellationToken cancellationToken)
    {
        var aspirePackageVersion = ExtractAspireVersionFromAssemblyMetadata();
        var aspirePackageRuntimeIdentifier = this.GetAspirePackageRuntimeIdentifier();

        var aspireOrchestrationPackageId = $"Aspire.Hosting.Orchestration.{aspirePackageRuntimeIdentifier}";
        var aspireDashboardPackageId = $"Aspire.Dashboard.Sdk.{aspirePackageRuntimeIdentifier}";

        this._downloadAspireOrchestrationPackageTask = this._nuGetPackageDownloader.DownloadAndExtractPackageAsync(aspireOrchestrationPackageId, aspirePackageVersion, cancellationToken);
        this._downloadAspireDashboardPackageTask = this._nuGetPackageDownloader.DownloadAndExtractPackageAsync(aspireDashboardPackageId, aspirePackageVersion, cancellationToken);
    }

    internal static string ExtractAspireVersionFromAssemblyMetadata()
    {
        const string aspireVersionMetadataKey = "aspireversion";

        // This assembly metadata attribute is generated in our csproj file using a custom target
        return typeof(AspireManager).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(x => string.Equals(x.Key, aspireVersionMetadataKey, StringComparison.OrdinalIgnoreCase))?.Value
            ?? throw new LeapException("Could not find the Aspire version in the assembly metadata");
    }

    private string GetAspirePackageRuntimeIdentifier()
    {
        var osPlatform = this._platformHelper.CurrentOS;
        var arch = this._platformHelper.ProcessArchitecture;

        // Those are all the package versions currently available for Aspire
        // https://www.nuget.org/packages?q=Aspire.Hosting.Orchestration.*
        return arch switch
        {
            Architecture.X64 when osPlatform == OSPlatform.Windows => "win-x64",
            Architecture.X64 when osPlatform == OSPlatform.Linux => "linux-x64",
            Architecture.X64 when osPlatform == OSPlatform.OSX => "osx-x64",

            Architecture.Arm64 when osPlatform == OSPlatform.Windows => "win-arm64",
            Architecture.Arm64 when osPlatform == OSPlatform.Linux => "linux-arm64",
            Architecture.Arm64 when osPlatform == OSPlatform.OSX => "osx-arm64",

            _ => throw new LeapException($"No Aspire package supported for platform '{osPlatform}' and process architecture '{arch}'")
        };
    }

    public async Task<DistributedApplication> StartAppHostAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Starting .NET Aspire dashboard...");

        DistributedApplication? app = null;
        try
        {
            app = await this.BuildDistributedApplicationAsync();
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (app != null)
            {
                await app.DisposeAsync();
            }

            throw new InvalidOperationException("Failed to start .NET Aspire", ex);
        }

        this._logger.LogInformation(".NET Aspire started successfully:");
        this._logger.LogInformation(" - Access the dashboard at {AspireDashboardUrl}", AspireDashboardUrlDefaultValue);
        this._logger.LogInformation(" - Send OpenTelemetry traces, metrics and logs to {AspireDashboardOtlpUrl} using GRPC", AspireDashboardOtlpUrlDefaultValue);

        return app;
    }

    private async Task<DistributedApplication> BuildDistributedApplicationAsync()
    {
        await this.UseCustomAspireWorkloadAsync();

        // TODO do we want to assign a well-known .NET Aspire port (same for the Aspire OTLP exporter port) instead of the default 18888 / 18889?
        // TODO do we want to proxy the Aspire dashboard URL to our YARP reverse proxy in order to have a nicer local domain URL?
        this.Builder.Services.TryAddLifecycleHook<NetworkingEnvironmentVariablesLifecycleHook>();
        this.Builder.Services.TryAddLifecycleHook<UseLeapCertificateForAspireDashboardLifecycleHook>();

        this.Builder.IgnoreConsoleTerminationSignals();
        this.Builder.ConfigureConsoleLogging(this._leapGlobalOptions.Value);
        this.Builder.ConfigureDashboard();

        return this.Builder.Build();
    }

    private async Task UseCustomAspireWorkloadAsync()
    {
        var orchestrationPackagePath = await this._downloadAspireOrchestrationPackageTask;
        var dashboardPackagePath = await this._downloadAspireDashboardPackageTask;

        // https://github.com/dotnet/aspire/blob/v8.0.0-preview.6.24214.1/src/Aspire.Hosting/Dcp/DcpOptions.cs#L123-L126
        var dcpBinPath = Path.Combine(orchestrationPackagePath, "tools", "ext", "bin");
        var dcpCliPath = Path.Combine(orchestrationPackagePath, "tools", "dcp");
        var dashboardPath = Path.Combine(dashboardPackagePath, "tools", "Aspire.Dashboard");

        if (this._platformHelper.CurrentOS == OSPlatform.Windows)
        {
            dcpCliPath += ".exe";
            dashboardPath += ".exe";
        }

        this.Builder.UseCustomAspireWorkload(new AspireWorkloadOptions(dcpBinPath, dcpCliPath, dashboardPath));
    }
}