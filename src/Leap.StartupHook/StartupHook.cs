using System.Diagnostics;
using System.Reflection;
using Leap.StartupHook;

// Inspired by https://github.com/dotnet/tye/blob/release/0.11.0/src/Microsoft.Tye.Hosting.Runtime/StartupHook.cs
internal sealed class StartupHook
{
    public static void Initialize()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException("The entry assembly name is null. This should never happen because we always have a running .NET application started with Leap local dev.");

        if (assemblyName == "dotnet" || assemblyName.StartsWith("dotnet-", StringComparison.Ordinal))
        {
            // We do not want to debug dotnet processes themselves but rather the application that is being run by dotnet
            // This includes: dotnet itself, dotnet-watch, dotnet-dev-certs, etc.
            return;
        }

        var serviceName = Environment.GetEnvironmentVariable("LEAP_SERVICE_NAME")
            ?? throw new InvalidOperationException("The service name is null. This should never happen because the environment variable should be added at Leap start time.");

        var serviceDebuggingSignalsPath = Environment.GetEnvironmentVariable("LEAP_STARTUP_HOOK_SIGNAL_FILE_PATH")
            ?? throw new InvalidOperationException("The service debugging signals path is null. This should never happen because the environment variable should be added at Leap start time.");

        if (!PersistentDotnetExecutableDebuggingState.IsDebuggingEnabled(serviceDebuggingSignalsPath))
        {
            return;
        }

        PersistentDotnetExecutableDebuggingState.DisableDebugging(serviceDebuggingSignalsPath);

        var currentProcess = Process.GetCurrentProcess();
        Console.WriteLine($"Waiting for debugger to attach to service '{serviceName}' with process ID {currentProcess.Id}.");

        while (!Debugger.IsAttached)
        {
            Thread.Sleep(1000);
        }

        Console.WriteLine("Debugger attached.");
    }
}