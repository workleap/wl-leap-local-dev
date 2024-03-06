using System.Reflection;

namespace Leap.Cli.Platform.Telemetry;

internal static class TelemetryConstants
{
    private const string FallbackAssemblyName = "leap";
    private const string FallbackAssemblyVersion = "unknown";

    public static readonly string AssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? FallbackAssemblyName;

    public static readonly string AssemblyVersion =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ??
        FallbackAssemblyVersion;

    public static class Attributes
    {
        public static class EndUser
        {
            // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.22.0/specification/trace/semantic_conventions/span-general.md#general-identity-attributes
            // https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-add-modify?tabs=net#set-the-user-id-or-authenticated-user-id
            public const string Id = "enduser.id";
        }

        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.22.0/specification/resource/semantic_conventions/process.md#process
        public static class Process
        {
            // Custom attribute inspired from the official "process" OTel attributes
            public const string ExitCode = "process.command_exit_code";
        }
    }

    public static class ActivityNames
    {
        public const string Root = "cli";
        public const string Process = "process";
        public const string PipelineStep = "step";
    }
}