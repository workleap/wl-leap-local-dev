using System.Reflection;
using System.Runtime.InteropServices;
using Aspire.Hosting.Lifecycle;
using CliWrap;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Aspire;

internal sealed class AspireManager : IAspireManager
{
    private const int AspireDashboardOtlpPort = 18889;
    private const int AspireDashboardPort = 18888;
    private const int AspireResourceServiceEndpointPort = 18887;

    // https://github.com/dotnet/aspire/blob/v8.0.0-preview.3.24105.21/src/Aspire.Dashboard/DashboardWebApplication.cs#L18-L21
    public static readonly string AspireDashboardOtlpUrlDefaultValue = $"https://localhost:{AspireDashboardOtlpPort}";
    public static readonly string AspireDashboardUrlDefaultValue = $"https://localhost:{AspireDashboardPort}";
    public static readonly string AspireResourceServiceEndpointUrl = $"https://localhost:{AspireResourceServiceEndpointPort}";

    public const string AspireOtlpDefaultApiKey = "leap";
    public const string DcpDefaultApiKey = "leap";

    private readonly ILogger _logger;
    private readonly INuGetPackageDownloader _nuGetPackageDownloader;
    private readonly IPlatformHelper _platformHelper;
    private readonly IPortManager _portManager;
    private readonly IDockerComposeManager _dockerComposeManager;
    private readonly PreferencesSettingsManager _preferencesSettingsManager;
    private readonly IOptions<LeapGlobalOptions> _leapGlobalOptions;
    private readonly ICliWrap _cliWrap;

    private Task<string> _downloadAspireOrchestrationPackageTask = Task.FromResult(string.Empty);
    private Task<string> _downloadAspireDashboardPackageTask = Task.FromResult(string.Empty);

    public AspireManager(
        ILogger<AspireManager> logger,
        INuGetPackageDownloader nuGetPackageDownloader,
        IPlatformHelper platformHelper,
        IPortManager portManager,
        IDockerComposeManager dockerComposeManager,
        PreferencesSettingsManager preferencesSettingsManager,
        IOptions<LeapGlobalOptions> leapGlobalOptions,
        ICliWrap cliWrap)
    {
        this._logger = logger;
        this._nuGetPackageDownloader = nuGetPackageDownloader;
        this._platformHelper = platformHelper;
        this._portManager = portManager;
        this._dockerComposeManager = dockerComposeManager;
        this._preferencesSettingsManager = preferencesSettingsManager;
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

        this.EnsureAspirePortsAreAvailable();

        DistributedApplication? app = null;
        try
        {
            app = await this.BuildDistributedApplicationAsync(cancellationToken);

            var dashboardReadinessAwaiter = app.Services.GetRequiredService<AspireDashboardReadinessAwaiter>();
            var dashboardReadyTask = dashboardReadinessAwaiter.WaitForDashboardReadyAsync(cancellationToken);

            var startAppTask = app.StartAsync(cancellationToken).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    this._logger.LogError(task.Exception, "Failed to start .NET Aspire");
                }
            }, cancellationToken, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);

            var firstTaskToFinish = await Task.WhenAny(dashboardReadyTask, startAppTask);

            // Task.WhenAny won't throw the finished task exception, so we do it manually
            await firstTaskToFinish;
        }
        catch (Exception ex)
        {
            if (app != null)
            {
                await app.DisposeAsync();
            }

            throw new InvalidOperationException("Failed to start .NET Aspire", ex);
        }

        this._logger.LogInformation("----------------------------------------------");
        this._logger.LogInformation("Dashboard available at {AspireDashboardUrl}", AspireDashboardUrlDefaultValue);
        this._logger.LogInformation("----------------------------------------------");

        return app;
    }

    private void EnsureAspirePortsAreAvailable()
    {
        this.EnsurePortIsAvailable(AspireDashboardOtlpPort);
        this.EnsurePortIsAvailable(AspireDashboardPort);
        this.EnsurePortIsAvailable(AspireResourceServiceEndpointPort);
    }

    private void EnsurePortIsAvailable(int port)
    {
        if (!this._portManager.IsPortAvailable(port))
        {
            throw new LeapException($"Failed to start .NET Aspire dashboard because the port {port} is already in use. Do you have another instance of Leap local dev running?");
        }
    }

    private async Task<DistributedApplication> BuildDistributedApplicationAsync(CancellationToken cancellationToken)
    {
        await this.UseCustomAspireWorkloadAsync(cancellationToken);

        this.Builder.Services.TryAddEventingSubscriber<UseLeapCertificateForAspireDashboardLifecycleHook>();
        this.Builder.Services.TryAddEventingSubscriber<DetectDotnetBuildRaceConditionErrorLifecycleHook>();
        this.Builder.Services.AddSingleton(this._preferencesSettingsManager);
        this.Builder.Services.AddSingleton(this._dockerComposeManager);

        this.Builder.Services.TryAddSingleton<AspireDashboardReadinessAwaiter>();
        this.Builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDistributedApplicationEventingSubscriber, AspireDashboardReadinessAwaiter>(
            x => x.GetRequiredService<AspireDashboardReadinessAwaiter>()));

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

        // Check if the Aspire dashboard needs code signing on macOS with Apple Silicon
        if (this._platformHelper.RequiresAppleCodeSigning())
        {
            await this.EnsureDashboardIsCodeSignedAsync(dashboardPath, cancellationToken);
        }

        this.Builder.UseCustomAspireWorkload(new AspireWorkloadOptions(dcpBinPath, dcpCliPath, dashboardPath));
    }

    private async Task EnsureDashboardIsCodeSignedAsync(string dashboardPath, CancellationToken cancellationToken)
    {
        this._logger.LogDebug("Checking if Aspire.Dashboard is properly code signed...");

        var isCodeSigned = await this._platformHelper.IsCodeSignedAsync(dashboardPath, cancellationToken);
        if (isCodeSigned)
        {
            this._logger.LogDebug("Aspire.Dashboard is already properly code signed");
            return;
        }

        this._logger.LogInformation("Attempting to code sign Aspire.Dashboard...");

        await this._platformHelper.CodeSignBinaryAsync(dashboardPath, cancellationToken);

        // Verify the signing was successful
        isCodeSigned = await this._platformHelper.IsCodeSignedAsync(dashboardPath, cancellationToken);
        if (isCodeSigned)
        {
            this._logger.LogInformation("Successfully code signed Aspire.Dashboard");
        }
        else
        {
            this._logger.LogWarning("Failed to code sign Aspire.Dashboard. The application may be terminated by macOS.");
        }
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