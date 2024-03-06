using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

internal sealed class PlatformHelper(ILogger<PlatformHelper> logger) : IPlatformHelper
{
    // Any Leap preview version (even built on the main branch) is considered unstable
    // Stable versions are built from tags in the format "x.y.z"
    private static readonly Regex StableLeapVersionRegex = new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.Compiled);

    private static readonly string[] KnownContinuousIntegrationEnvironmentVariables =
    [
        "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI", // Azure Pipelines
        "GITHUB_ACTIONS", // GitHub Actions
        "TEAMCITY", // TeamCity
    ];

    private readonly Lazy<OSPlatform> _lazyOsPlatform = new Lazy<OSPlatform>(GetCurrentOS);
    private readonly Lazy<bool> _lazyIsCurrentProcessElevated = new Lazy<bool>(IsCurrentProcessElevatedInternal);
    private readonly Lazy<bool> _lazyIsRunningOnStableVersion = new Lazy<bool>(IsRunningOnStableVersionInternal);

    public OSPlatform CurrentOS => this._lazyOsPlatform.Value;

    public Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

    public bool IsRunningOnBuildAgent => KnownContinuousIntegrationEnvironmentVariables
        .Any(x => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x)));

    public bool IsRunningInReleaseConfiguration =>
#if RELEASE
        true;
#else
        false;
#endif

    public bool IsRunningOnStableVersion => this._lazyIsRunningOnStableVersion.Value;

    public bool IsCurrentProcessElevated => this._lazyIsCurrentProcessElevated.Value;

    private static OSPlatform GetCurrentOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        throw new PlatformNotSupportedException();
    }

    [DllImport("libc")]
    [SuppressMessage("Security", "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes", Justification = "Not necessary for libc.")]
    private static extern uint geteuid();

    private static bool IsCurrentProcessElevatedInternal()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // https://github.com/dotnet/sdk/blob/v6.0.100/src/Cli/dotnet/Installer/Windows/WindowsUtils.cs#L38
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // https://github.com/dotnet/maintenance-packages/blob/62823150914410d43a3fd9de246d882f2a21d5ef/src/Common/tests/TestUtilities/System/PlatformDetection.Unix.cs#L58
        // 0 is the ID of the root user
        return geteuid() == 0;
    }

    private static bool IsRunningOnStableVersionInternal()
    {
        var assemblyVersion =
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString();

        return assemblyVersion is not null && StableLeapVersionRegex.IsMatch(assemblyVersion);
    }

    public async Task StartLeapElevatedAsync(string[] args, CancellationToken cancellationToken)
    {
        var currentProcessPath = Environment.ProcessPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.ChangeExtension(typeof(Program).Assembly.Location, "exe")
            : Path.ChangeExtension(typeof(Program).Assembly.Location, null));

        var processStartInfo = CreateProcessStartInfo(currentProcessPath, args);

        logger.LogDebug("Starting elevated process '{ProcessPath}' with arguments '{Arguments}'", processStartInfo.FileName, string.Join(' ', processStartInfo.ArgumentList));

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start process.");

        await process.WaitForExitAsync(cancellationToken);
    }

    private static ProcessStartInfo CreateProcessStartInfo(string processPath, string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConfigureProcessStartInfoForWindows(startInfo, processPath, args);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ConfigureProcessStartInfoForLinux(startInfo, processPath, args);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ConfigureProcessStartInfoForMacOS(startInfo, processPath, args);
        }
        else
        {
            // This will never happen because we already checked the OS platform
            throw new PlatformNotSupportedException();
        }

        return startInfo;
    }

    private static void ConfigureProcessStartInfoForWindows(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.Verb = "runas";
        startInfo.FileName = processPath;

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
    }

    private static void ConfigureProcessStartInfoForLinux(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.FileName = "sudo";
        startInfo.ArgumentList.Add(processPath);

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
    }

    private static void ConfigureProcessStartInfoForMacOS(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.FileName = "osascript";
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"do shell script \"{processPath} {string.Join(' ', args)}\" with prompt \"Leap\" with administrator privileges");
    }
}