using Aspire.Hosting.Lifecycle;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

internal sealed class AspireManager : IAspireManager
{
    // https://github.com/dotnet/aspire/blob/v8.0.0-preview.3.24105.21/src/Aspire.Dashboard/DashboardWebApplication.cs#L18-L21
    private const string AspireDashboardOtlpUrlDefaultValue = "http://localhost:18889";
    private const string AspireDashboardUrlDefaultValue = "http://localhost:18888";

    private readonly ILogger _logger;

    public AspireManager(ILogger<AspireManager> logger)
    {
        this._logger = logger;

        var builder = DistributedApplication.CreateBuilder();

        // TODO do we want to assign a well-known .NET Aspire port (same for the Aspire OTLP exporter port) instead of the default 18888 / 18889?
        // TODO do we want to proxy the Aspire dashboard URL to our YARP reverse proxy in order to have a nicer local domain URL?
        builder.Services.AddSingleton<IDistributedApplicationLifecycleHook, NetworkingEnvironmentVariablesLifecycleHook>();
        builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();

        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new SimpleColoredConsoleLoggerProvider());
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",

            // .NET Aspire is too verbose by default
            ["Logging:LogLevel:Aspire.Hosting"] = "Warning",

            // Silence the "could not remove process's standard output file" error that sometimes occurs when stopping the Aspire hosting process
            // It happens even with a brand new blank ASP.NET Core web API project
            ["Aspire.Hosting.Dcp.dcpctrl.ExecutableReconciler"] = "None",
        });

        this.Builder = builder;
    }

    public IDistributedApplicationBuilder Builder { get; }

    public async Task<IAsyncDisposable> StartAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Starting .NET Aspire dashboard...");

        DistributedApplication? app = null;
        try
        {
            app = this.Builder.Build();
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (app != null)
            {
                await app.DisposeAsync();
            }

            throw new LeapException("Failed to start .NET Aspire dashboard", ex);
        }

        this._logger.LogInformation(".NET Aspire started successfully:");
        this._logger.LogInformation(" - Access the dashboard at {AspireDashboardUrl}", AspireDashboardUrlDefaultValue);
        this._logger.LogInformation(" - Send OpenTelemetry traces, metrics and logs to {AspireDashboardOtlpUrl}", AspireDashboardOtlpUrlDefaultValue);

        return app;
    }
}
