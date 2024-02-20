using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;

namespace Leap.Cli.Pipeline;

internal sealed class StartReverseProxyPipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger _logger;

    private WebApplication? _app;

    public StartReverseProxyPipelineStep(
        IFeatureManager featureManager,
        ILogger<StartReverseProxyPipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(StartReverseProxyPipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        if (state.Services.Count == 0)
        {
            return;
        }

        await this.StartReverseProxyAsync(state, cancellationToken);
    }

    private async Task StartReverseProxyAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });

        // Suppress Ctrl+C, SIGINT, and SIGTERM signals because already handled by System.CommandLine
        // through the cancellation token that is passed to the pipeline step.
        builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();

        builder.WebHost.UseUrls("https://+:" + Constants.LeapReverseProxyPort);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kestrel:Certificates:Default:Path"] = Constants.LocalCertificateCrtFilePath,
            ["Kestrel:Certificates:Default:KeyPath"] = Constants.LocalCertificateKeyFilePath,

            ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
            ["Logging:LogLevel:Yarp.ReverseProxy"] = "Warning",
        });

        // TODO change configuration, logging
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new SimpleColoredConsoleLoggerProvider());

        // TODO For now YARP config is statically built but we could make it dynamic based on up and down services
        var clusters = new List<ClusterConfig>();
        var routes = new List<RouteConfig>();

        // TODO honor path specify in services

        // TODO We would need to ensure there's no duplicate, overlaping paths, etc. early on
        foreach (var (serviceName, service) in state.Services)
        {
            // Remote services aren't proxied and instead directly accessed by other services
            if (service.ActiveBinding is RemoteBinding)
            {
                continue;
            }

            if (service.Ingress.Host == "127.0.0.1")
            {
                // TODO do we need to reverse proxy something that isn't bound to a specific host?
                continue;
            }

            var routeId = "route_" + serviceName;
            var clusterId = "cluster_" + serviceName;

            // TODO do we need as many clusters and routes?
            var cluster = new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    [clusterId + "/destination"] = new DestinationConfig
                    {
                        Address = service.ActiveBinding?.Protocol + "://127.0.0.1:" + service.Ingress.InternalPort,
                    },
                },
            };

            var route = new RouteConfig
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Path = "{**catch-all}", // TODO use specified ingress path
                    Hosts = new[] { service.Ingress.Host! },
                },
            };

            clusters.Add(cluster);
            routes.Add(route);
        }

        builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters)
#pragma warning disable CA5359
            // This might seems like a security issue, but it's not.
            // We trust the subscribers' certificates as this emulator is intended to be used in a local environment,
            // where certificates are mostly self-signed and not trusted by the Docker container.
            .ConfigureHttpClient((_, handler) => handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true);
#pragma warning restore CA5359

        this._app = builder.Build();

        this._app.MapReverseProxy();

        this._logger.LogInformation("Starting Leap reverse proxy using HTTPS on port {Port}...", Constants.LeapReverseProxyPort);

        await this._app.StartAsync(cancellationToken);

        this._logger.LogInformation("Leap reverse proxy started successfully");
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._app != null)
        {
            await this._app.DisposeAsync();
        }
    }
}
