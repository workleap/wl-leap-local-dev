using Aspire.Hosting;
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
    internal const string AspireDashboardOtlpUrlDefaultValue = "https://localhost:18889";
    internal const string AspireDashboardUrlDefaultValue = "https://localhost:18888";
    internal const string AspireResourceServiceEndpointUrl = "https://localhost:18887";

    private readonly ILogger _logger;

    public AspireManager(ILogger<AspireManager> logger)
    {
        this._logger = logger;

        var builder = DistributedApplication.CreateBuilder();
        this.Builder = builder;
    }

    public IDistributedApplicationBuilder Builder { get; }

    public async Task<DistributedApplication> StartAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Starting .NET Aspire dashboard...");

        DistributedApplication? app = null;
        try
        {
            app = this.BuildDistributedApplication();
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
        this._logger.LogInformation(" - Send OpenTelemetry traces, metrics and logs to {AspireDashboardOtlpUrl} using GRPC", AspireDashboardOtlpUrlDefaultValue);

        return app;
    }

    private DistributedApplication BuildDistributedApplication()
    {
        // TODO do we want to assign a well-known .NET Aspire port (same for the Aspire OTLP exporter port) instead of the default 18888 / 18889?
        // TODO do we want to proxy the Aspire dashboard URL to our YARP reverse proxy in order to have a nicer local domain URL?
        this.Builder.Services.AddSingleton<IDistributedApplicationLifecycleHook, NetworkingEnvironmentVariablesLifecycleHook>();
        this.Builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();

        this.Builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new SimpleColoredConsoleLoggerProvider());
        });

        this.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",

            // .NET Aspire is too verbose by default
            ["Logging:LogLevel:Aspire.Hosting"] = "Warning",

            // Silence the "could not remove process's standard output file" error that sometimes occurs when stopping the Aspire hosting process
            // It happens even with a brand new blank ASP.NET Core web API project
            ["Aspire.Hosting.Dcp.dcpctrl.ExecutableReconciler"] = "None",
        });

        return this.Builder.Build();
    }
}