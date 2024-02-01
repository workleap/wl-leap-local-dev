using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Principal;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

internal sealed class PlatformHelper(ICliWrap cliWrap, ILogger<PlatformHelper> logger) : IPlatformHelper
{
    private readonly Lazy<OSPlatform> _lazyOsPlatform = new Lazy<OSPlatform>(GetCurrentOS);

    public OSPlatform CurrentOS => this._lazyOsPlatform.Value;

    public Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

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

    public async Task MakeExecutableAsync(string targetPath, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var chmod = new Command("chmod").WithValidation(CommandResultValidation.None).WithArguments(["+x", targetPath]);
            await cliWrap.ExecuteBufferedAsync(chmod, cancellationToken);
        }
    }

    [DllImport("libc")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "That's the name of the P/Invokes function.")]
    [SuppressMessage("Security", "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes", Justification = "Not necessary for libc.")]
    private static extern uint geteuid();

    public bool IsCurrentProcessElevated()
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