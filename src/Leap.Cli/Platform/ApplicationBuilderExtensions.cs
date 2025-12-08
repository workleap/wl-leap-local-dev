using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Leap.Cli.Platform;

internal static class ApplicationBuilderExtensions
{
    public static WebApplicationBuilder IgnoreConsoleTerminationSignals(this WebApplicationBuilder builder)
    {
        builder.Services.IgnoreConsoleTerminationSignals();
        return builder;
    }

    public static IDistributedApplicationBuilder IgnoreConsoleTerminationSignals(this IDistributedApplicationBuilder builder)
    {
        builder.Services.IgnoreConsoleTerminationSignals();
        return builder;
    }

    private static IServiceCollection IgnoreConsoleTerminationSignals(this IServiceCollection services)
    {
        // Suppresses Ctrl+C, SIGINT, and SIGTERM signals in cases where it's already handled by System.CommandLine
        // through the cancellation token that is passed to consuming methods.
        return services.AddSingleton<IHostLifetime, NoopHostLifetime>();
    }

    private sealed class NoopHostLifetime : IHostLifetime
    {
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}