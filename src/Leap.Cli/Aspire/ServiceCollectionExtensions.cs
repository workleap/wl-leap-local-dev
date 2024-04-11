using System.Reflection;
using Aspire.Hosting;
using Leap.Cli.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Aspire;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// The DcpOptions class is internal, so we need to use reflection to set its properties. We need to do this so the
    /// path to certain key Aspire executables can be set at runtime.
    /// </summary>
    public static IServiceCollection ConfigureInternalOptions(this IServiceCollection services, DcpConfig dcpConfig)
    {
        var dcpOptionsType = typeof(DistributedApplication).Assembly.GetType("Aspire.Hosting.Dcp.DcpOptions")
                             ?? throw new InvalidOperationException("Type 'Aspire.Hosting.Dcp.DcpOptions' was not found when trying to configure the Aspire distributed application host.");

        var dcpOptionsCliPathSetter = dcpOptionsType.GetProperty("CliPath", BindingFlags.Public | BindingFlags.Instance)?.SetMethod
                                      ?? throw new InvalidOperationException("Property 'CliPath' not found on type 'Aspire.Hosting.Dcp.DcpOptions'");

        var dcpOptionsDashboardPathSetter = dcpOptionsType.GetProperty("DashboardPath", BindingFlags.Public | BindingFlags.Instance)?.SetMethod
                                            ?? throw new InvalidOperationException("Property 'DashboardPath' not found on type 'Aspire.Hosting.Dcp.DcpOptions'");

        var dcpOptionsInstance = Activator.CreateInstance(dcpOptionsType);
        dcpOptionsCliPathSetter.Invoke(dcpOptionsInstance, [dcpConfig.DcpCliPath]);
        dcpOptionsDashboardPathSetter.Invoke(dcpOptionsInstance, [dcpConfig.DashboardPath]);

        var msExtOptionsType = typeof(Options);
        var msExtOptionsCreateMethod = msExtOptionsType.GetMethod(nameof(Options.Create), BindingFlags.Static | BindingFlags.Public)
                                       ?? throw new InvalidOperationException($"Method '{nameof(Options.Create)}' not found");

        var msExtOptionsCreateMethodForDcpOptions = msExtOptionsCreateMethod.MakeGenericMethod(dcpOptionsType);
        var dcpOptionsWrapper = msExtOptionsCreateMethodForDcpOptions.Invoke(obj: null, parameters: [dcpOptionsInstance])
                                ?? throw new InvalidOperationException("Unable to create an instance of 'Aspire.Hosting.Dcp.DcpOptions'");

        var msExtOptionsInterface = typeof(IOptions<>).MakeGenericType(dcpOptionsType);

        services.AddSingleton(msExtOptionsInterface, dcpOptionsWrapper);

        return services;
    }
}