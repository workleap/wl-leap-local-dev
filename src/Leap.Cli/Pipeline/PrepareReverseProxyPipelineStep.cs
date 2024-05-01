using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Leap.Cli.Aspire;
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

internal sealed class PrepareReverseProxyPipelineStep(IAspireManager aspireManager) : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (state.Services.Count == 0)
        {
            return Task.CompletedTask;
        }

        aspireManager.Builder.Services.TryAddLifecycleHook<HostYarpInAspireLifecycleHook>();

        aspireManager.Builder.AddResource(new YarpResource(state))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Reverse proxy",
                State = "Starting",
                Properties = [new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, "leap")]
            })
            .ExcludeFromManifest();

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private sealed class YarpResource(ApplicationState state) : Resource("yarp")
    {
        public ApplicationState State { get; } = state;
    }

    private sealed class HostYarpInAspireLifecycleHook(ResourceNotificationService notificationService, ResourceLoggerService loggerService)
        : IDistributedApplicationLifecycleHook, IAsyncDisposable
    {
        private WebApplication? _app;

        public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            var yarpResource = appModel.Resources.OfType<YarpResource>().Single();
            var yarpLogger = loggerService.GetLogger(yarpResource);

            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    EnvironmentName = Environments.Production,
                });

                builder.IgnoreConsoleTerminationSignals();

                builder.WebHost.UseUrls("https://+:" + Constants.LeapReverseProxyPort);

                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kestrel:Certificates:Default:Path"] = Constants.LocalCertificateCrtFilePath,
                    ["Kestrel:Certificates:Default:KeyPath"] = Constants.LocalCertificateKeyFilePath,

                    ["Logging:LogLevel:Default"] = "Information",
                });

                builder.Logging.ClearProviders();
                builder.Logging.AddResourceLogger(yarpLogger);

                List<RouteConfig> routes = [];
                List<ClusterConfig> clusters = [];
                HashSet<UrlSnapshot> urls = [];

                // TODO We would need to ensure there's no duplicate, overlapping paths, etc. early on
                foreach (var (serviceName, service) in yarpResource.State.Services)
                {
                    if (service.ActiveRunner is RemoteRunner)
                    {
                        continue;
                    }

                    var host = service.Ingress.Host!;
                    var path = service.Ingress.Path!;

                    var isHostLocalhost = host == "127.0.0.1" || "localhost".Equals(host, StringComparison.OrdinalIgnoreCase);
                    if (isHostLocalhost)
                    {
                        continue;
                    }

                    urls.Add(new UrlSnapshot(serviceName, $"https://{host}:{Constants.LeapReverseProxyPort}{path}", IsInternal: false));

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
                                Address = service.ActiveRunner?.Protocol + "://127.0.0.1:" + service.Ingress.InternalPort,
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
                    // This isn't a security misconfiguration. We're in a local development environment
                    // and we want to allow self-signed certificates that are not trusted by the system.
                    .ConfigureHttpClient((_, handler) => handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true);
#pragma warning restore CA5359

                this._app = builder.Build();

                this._app.MapReverseProxy();

                await this._app.StartAsync(cancellationToken);

                await notificationService.PublishUpdateAsync(yarpResource, state => state with
                {
                    State = "Running",
                    Urls = [.. urls],
                });
            }
            catch (Exception ex)
            {
                yarpLogger.LogError(ex, "An error occured while starting Azure CLI credentials proxy for Docker");

                await notificationService.PublishUpdateAsync(yarpResource, state => state with
                {
                    State = "Finished"
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this._app != null)
            {
                await this._app.StopAsync(CancellationToken.None);
                await this._app.DisposeAsync();
            }
        }
    }
}
