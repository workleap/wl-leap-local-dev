using System.Reflection;
using System.Runtime.InteropServices;
using Aspire.Hosting.Lifecycle;
using CliWrap;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Hosting;
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
    private readonly ICliWrap _cliWrap;

    private Task<string> _downloadAspireOrchestrationPackageTask = Task.FromResult(string.Empty);
    private Task<string> _downloadAspireDashboardPackageTask = Task.FromResult(string.Empty);

    public AspireManager(
        ILogger<AspireManager> logger,
        INuGetPackageDownloader nuGetPackageDownloader,
        IPlatformHelper platformHelper,
        IOptions<LeapGlobalOptions> leapGlobalOptions,
        ICliWrap cliWrap)
    {
        this._logger = logger;
        this._nuGetPackageDownloader = nuGetPackageDownloader;
        this._platformHelper = platformHelper;
        this._leapGlobalOptions = leapGlobalOptions;
        this._cliWrap = cliWrap;

        this.Builder = DistributedApplication.CreateBuilder();

        // Starting the Aspire app host in development mode enabled various local development features such as
        // high-frequency OpenTelemetry push intervals, and no sampling.
        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/OtlpConfigurationExtensions.cs#L55-L65
        this.Builder.Environment.EnvironmentName = Environments.Development;
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
            app = await this.BuildDistributedApplicationAsync(cancellationToken);
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

    private async Task<DistributedApplication> BuildDistributedApplicationAsync(CancellationToken cancellationToken)
    {
        await this.UseCustomAspireWorkloadAsync(cancellationToken);

        // TODO do we want to assign a well-known .NET Aspire port (same for the Aspire OTLP exporter port) instead of the default 18888 / 18889?
        // TODO do we want to proxy the Aspire dashboard URL to our YARP reverse proxy in order to have a nicer local domain URL?
        this.Builder.Services.TryAddLifecycleHook<UseLeapCertificateForAspireDashboardLifecycleHook>();
        this.Builder.Services.TryAddLifecycleHook<DetectDotnetBuildRaceConditionErrorLifecycleHook>();

        this.Builder.IgnoreConsoleTerminationSignals();
        this.Builder.ConfigureConsoleLogging(this._leapGlobalOptions.Value);
        this.Builder.ConfigureDashboard();

        return this.Builder.Build();
    }

    private async Task UseCustomAspireWorkloadAsync(CancellationToken cancellationToken)
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

        await this.VerifyDcpWorksAsync(dcpCliPath, cancellationToken);

        this.Builder.UseCustomAspireWorkload(new AspireWorkloadOptions(dcpBinPath, dcpCliPath, dashboardPath));
    }

    private async Task VerifyDcpWorksAsync(string dcpCliPath, CancellationToken cancellationToken)
    {
        // On Azure DevOps Windows CI agents, Aspire SOMETIMES fails to start because "dcp.exe info" returns a non-zero exit code:
        // "System.InvalidOperationException: Command C:\Users\VssAdministrator\.leap\generated\nuget-packages\Aspire.Hosting.Orchestration.win-x64.8.0.1\tools\dcp.exe info returned non-zero exit code -1"
        // The code that throws: https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dcp/DcpDependencyCheck.cs#L34
        //
        // This behavior hasn't been observed anywhere else. It seems to happen because it takes too long for "dcp info" to exit successfully.
        // To mitigate this, we will attempt to execute the command ourselves and ensure it returns a zero exit code.
        if (this._platformHelper.CurrentOS == OSPlatform.Windows && this._platformHelper.IsRunningOnBuildAgent)
        {
            const int maxRetryCount = 5;
            for (var retryCount = 1; retryCount <= maxRetryCount; retryCount++)
            {
                this._logger.LogTrace("Checking if DCP works (attempt {RetryCount}/{MaxRetryCount})", retryCount, maxRetryCount);

                var dcpInfoCommand = new Command(dcpCliPath).WithArguments("info").WithValidation(CommandResultValidation.None);
                var dcpInfoResult = await this._cliWrap.ExecuteBufferedAsync(dcpInfoCommand, cancellationToken);

                if (dcpInfoResult.IsSuccess)
                {
                    return;
                }

                this._logger.LogTrace("DCP check failed with exit code {ExitCode}, stdout: {StdOut}, stderr: {StdErr}", dcpInfoResult.ExitCode, dcpInfoResult.StandardOutput, dcpInfoResult.StandardError);

                if (retryCount < maxRetryCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                else
                {
                    // That's ok, .NET Aspire will attempt the same thing and throw an exception if it fails
                }
            }
        }
    }
}