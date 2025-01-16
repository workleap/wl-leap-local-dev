using System.Reflection;
using Aspire.Hosting.Lifecycle;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Aspire;

internal static class DistributedApplicationBuilderExtensions
{
    public static IDistributedApplicationBuilder ConfigureConsoleLogging(this IDistributedApplicationBuilder builder, LeapGlobalOptions leapGlobalOptions)
    {
        builder.Services.AddSingleton(Options.Create(leapGlobalOptions));
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddColoredConsoleLogger(LoggingSource.Aspire);
        });

        // Makes sure that the aspire distributed application hosts console logs are properly configured.
        // Not too verbose, yet provides enough information to be useful.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",

            // .NET Aspire is too verbose by default
            ["Logging:LogLevel:Aspire.Hosting"] = "Warning",
        });

        if (leapGlobalOptions.Verbosity == LoggerVerbosity.Diagnostic)
        {
            builder.Services.TryAddLifecycleHook<EnableAspireDashboardDiagnosticLoggingLifecycleHook>();
        }

        return builder;
    }

    public static IDistributedApplicationBuilder ConfigureDashboard(this IDistributedApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Configure the Aspire dashboard URLs
            ["ASPNETCORE_URLS"] = AspireManager.AspireDashboardUrlDefaultValue,
            ["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = AspireManager.AspireDashboardOtlpUrlDefaultValue,
            ["DOTNET_RESOURCE_SERVICE_ENDPOINT_URL"] = AspireManager.AspireResourceServiceEndpointUrl,

            // Disable authentication on the Dashboard
            ["AppHost:BrowserToken"] = "",

            // Hardcoded OTLP API key to prevent Aspire from generating a random one at each run
            // We can use it when manually configuring containers with Docker Compose prior to starting the dashboard
            ["AppHost:OtlpApiKey"] = AspireManager.AspireOtlpDefaultApiKey,

            // Hardcoded DCP API key, so we can use request DCP to get status of services and their logs from tests
            ["AppHost:ResourceService:AuthMode"] = "ApiKey",
            ["AppHost:ResourceService:ApiKey"] = AspireManager.DcpDefaultApiKey,
        });

        return builder;
    }

    /// <summary>
    /// The DcpOptions class is internal, so we need to use reflection to set its properties. We need to do this so the
    /// path to certain key Aspire executables can be set at runtime.
    /// </summary>
    public static IDistributedApplicationBuilder UseCustomAspireWorkload(this IDistributedApplicationBuilder builder, AspireWorkloadOptions aspireWorkloadOptions)
    {
        var dcpOptionsType = typeof(DistributedApplication).Assembly.GetType("Aspire.Hosting.Dcp.DcpOptions")
            ?? throw new InvalidOperationException("Type 'Aspire.Hosting.Dcp.DcpOptions' was not found when trying to configure the Aspire distributed application host.");

        var dcpOptionsBinPathSetter = dcpOptionsType.GetProperty("BinPath", BindingFlags.Public | BindingFlags.Instance)?.SetMethod
            ?? throw new InvalidOperationException("Property 'BinPath' not found on type 'Aspire.Hosting.Dcp.DcpOptions'");

        var dcpOptionsCliPathSetter = dcpOptionsType.GetProperty("CliPath", BindingFlags.Public | BindingFlags.Instance)?.SetMethod
            ?? throw new InvalidOperationException("Property 'CliPath' not found on type 'Aspire.Hosting.Dcp.DcpOptions'");

        var dcpOptionsDashboardPathSetter = dcpOptionsType.GetProperty("DashboardPath", BindingFlags.Public | BindingFlags.Instance)?.SetMethod
            ?? throw new InvalidOperationException("Property 'DashboardPath' not found on type 'Aspire.Hosting.Dcp.DcpOptions'");

        var dcpOptionsInstance = Activator.CreateInstance(dcpOptionsType);
        dcpOptionsBinPathSetter.Invoke(dcpOptionsInstance, [aspireWorkloadOptions.DcpBinPath]);
        dcpOptionsCliPathSetter.Invoke(dcpOptionsInstance, [aspireWorkloadOptions.DcpCliPath]);
        dcpOptionsDashboardPathSetter.Invoke(dcpOptionsInstance, [aspireWorkloadOptions.DashboardPath]);

        var msExtOptionsType = typeof(Options);
        var msExtOptionsCreateMethod = msExtOptionsType.GetMethod(nameof(Options.Create), BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Method '{nameof(Options.Create)}' not found");

        var msExtOptionsCreateMethodForDcpOptions = msExtOptionsCreateMethod.MakeGenericMethod(dcpOptionsType);
        var dcpOptionsWrapper = msExtOptionsCreateMethodForDcpOptions.Invoke(obj: null, parameters: [dcpOptionsInstance])
            ?? throw new InvalidOperationException("Unable to create an instance of 'Aspire.Hosting.Dcp.DcpOptions'");

        var msExtOptionsInterface = typeof(IOptions<>).MakeGenericType(dcpOptionsType);

        builder.Services.AddSingleton(msExtOptionsInterface, dcpOptionsWrapper);

        // Setting this value in the configuration makes it so the DcpOptions values are not set from assembly metadata
        // https://github.com/dotnet/aspire/blob/98176786b3737940860e8269a36b405b01eeb6e9/src/Aspire.Hosting/Dcp/DcpOptions.cs#L89
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Required so that the DcpOptions are not configured with assembly metadata
            ["DcpPublisher:CliPath"] = aspireWorkloadOptions.DcpCliPath,
        });

        return builder;
    }
}