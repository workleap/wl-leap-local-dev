using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.InteropServices;
using Leap.Cli.Aspire;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Configuration;

namespace Leap.Cli.Pipeline;

/// <summary>
/// This step exist so we can configure the DcpOptions which are required to start .NET Aspire. These options are set internally
/// within the Aspire Sdk from values computed at build time and set as assembly metadata. These options contain the path to certain
/// key executables for Aspire. Since we build and package Leap only once, the computed values in the assembly metadata will be specific
/// for the platform Leap was built on. However, Leap can be run on different platforms, that's why we need to have the DcpOptions
/// set at runtime with the proper values for the user's platform.
/// </summary>
internal sealed class ConfigureAspireDcpOptions : IPipelineStep
{
    private readonly IPlatformHelper _platformHelper;
    private readonly IAspireManager _aspireManager;
    private readonly IFileSystem _fileSystem;

    private string AspireHostingVersion
    {
        get
        {
            var versionAttribute = typeof(DistributedApplication).Assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;

            // InformationalVersion format is like this: "8.0.0-preview.3.24105.21+<commit_sha>"
            // We're only interested in the first part
            var assemblyVersion = versionAttribute?.InformationalVersion.Split('+')[0];
            return assemblyVersion ?? throw new LeapException("Could not determine the Aspire.Hosting version");
        }
    }

    public ConfigureAspireDcpOptions(IPlatformHelper platformHelper, IAspireManager aspireManager, IFileSystem fileSystem)
    {
        this._platformHelper = platformHelper;
        this._aspireManager = aspireManager;
        this._fileSystem = fileSystem;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._fileSystem.Path.Exists(this._platformHelper.DotnetRootPath))
        {
            throw new LeapException($"Could not find the .NET root path at the following location: {this._platformHelper.DotnetRootPath}. If .NET is installed in another location, you can set the DOTNET_ROOT environment variable to point to this location.");
        }

        var dcpConfig = this.GetDcpConfig();

        if (!this._fileSystem.File.Exists(dcpConfig.DcpCliPath))
        {
            throw new LeapException(AspireExecutableExceptionMessage(dcpConfig.DcpCliPath));
        }

        if (!this._fileSystem.File.Exists(dcpConfig.DashboardPath))
        {
            throw new LeapException(AspireExecutableExceptionMessage(dcpConfig.DashboardPath));
        }

        static string AspireExecutableExceptionMessage(string path) => $"File '{path}' not found. Check if Aspire workload is installed with ‘dotnet workload list’. And make sure it's the same version that Leap is trying to use. " +
                                                                       $"From here you can either install with ‘dotnet workload install aspire’ or update with ‘dotnet workload update aspire’.";

        // Setting this value in the configuration makes it so the DcpOptions values are not set from assembly metadata
        // https://github.com/dotnet/aspire/blob/98176786b3737940860e8269a36b405b01eeb6e9/src/Aspire.Hosting/Dcp/DcpOptions.cs#L89
        this._aspireManager.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Required so that the DcpOptions are not configured with assembly metadata
            ["DcpPublisher:CliPath"] = dcpConfig.DcpCliPath,
        });

        this._aspireManager.Builder.Services.ConfigureInternalOptions(dcpConfig);

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// DcpCli path and dashboard path are build copying how it's done in Aspire to set the assembly metadata
    /// https://github.com/dotnet/aspire/blob/4aca143485fdde29483df9e8cce6113135f2260c/eng/dashboardpack/Sdk.targets
    /// https://github.com/dotnet/aspire/blob/4aca143485fdde29483df9e8cce6113135f2260c/eng/dcppack/Aspire.Hosting.Orchestration.targets
    /// </summary>
    private DcpConfig GetDcpConfig()
    {
        var dotnetPacksPath = Path.Combine(this._platformHelper.DotnetRootPath!, "packs");
        var osPlatform = this._platformHelper.CurrentOS;
        var arch = this._platformHelper.ProcessArchitecture;

        // Those are all the package versions currently available for Aspire
        // https://www.nuget.org/packages?q=Aspire.Hosting.Orchestration.*
        var aspirePackageVersion = arch switch
        {
            Architecture.X64 when osPlatform == OSPlatform.Windows => "win-x64",
            Architecture.X64 when osPlatform == OSPlatform.Linux => "linux-x64",
            Architecture.X64 when osPlatform == OSPlatform.OSX => "osx-x64",

            Architecture.Arm64 when osPlatform == OSPlatform.Windows => "win-arm64",
            Architecture.Arm64 when osPlatform == OSPlatform.Linux => "linux-arm64",
            Architecture.Arm64 when osPlatform == OSPlatform.OSX => "osx-arm64",

            _ => throw new LeapException($"No Aspire package supported for platform '{osPlatform}' and process architecture '{arch}'")
        };

        // The Dcp CLI is verified here
        // https://github.com/dotnet/aspire/blob/88406425f85daccec9b5cb3f22d5c8d07ebf3a8d/src/Aspire.Hosting/Dcp/DcpHostService.cs#L267
        var dcpCliPath = Path.Combine(
            dotnetPacksPath,
            $"Aspire.Hosting.Orchestration.{aspirePackageVersion}",
            this.AspireHostingVersion,
            "tools",
            "dcp");

        // The dashboard path is required in order to start the .NET Aspire dashboard
        // https://github.com/dotnet/aspire/blob/88406425f85daccec9b5cb3f22d5c8d07ebf3a8d/src/Aspire.Hosting/Dcp/ApplicationExecutor.cs#L166
        var dashboardPath = Path.Combine(
            dotnetPacksPath,
            $"Aspire.Dashboard.Sdk.{aspirePackageVersion}",
            this.AspireHostingVersion,
            "tools",
            "Aspire.Dashboard");

        if (osPlatform == OSPlatform.Windows)
        {
            dcpCliPath += ".exe";
            dashboardPath += ".exe";
        }

        return new DcpConfig(dcpCliPath, dashboardPath);
    }
}

internal record DcpConfig(string DcpCliPath, string DashboardPath);
