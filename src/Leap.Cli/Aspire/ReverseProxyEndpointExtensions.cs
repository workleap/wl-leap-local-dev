using Aspire.Hosting.Lifecycle;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

internal static class ReverseProxyEndpointExtensions
{
    public static IResourceBuilder<T> WithReverseProxyUrl<T>(this IResourceBuilder<T> builder, Service service)
        where T : IResource
    {
        // no-op if the services doesn't have a reverse proxy URL
        return !service.Ingress.Host.IsLocalhost ? WithReverseProxyUrl(builder, service.ReverseProxyUrl) : builder;
    }

    public static IResourceBuilder<T> WithReverseProxyUrl<T>(this IResourceBuilder<T> builder, string url)
        where T : IResource
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<ReverseProxyEndpointLifecycleHook>();
        return builder.WithAnnotation(new ReverseProxyEndpointAnnotation(url), ResourceAnnotationMutationBehavior.Replace);
    }

    private sealed class ReverseProxyEndpointAnnotation(string reverseProxyUrl) : IResourceAnnotation
    {
        public string ReverseProxyUrl { get; } = reverseProxyUrl;
    }

    private sealed class ReverseProxyEndpointLifecycleHook(ResourceNotificationService notificationService, ILogger<ReverseProxyEndpointLifecycleHook> logger) : IDistributedApplicationLifecycleHook, IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Task? _watchTask;

        public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            this._watchTask = this.WatchResourceAndCorrectUrlsAsync(this._cts.Token);
            return Task.CompletedTask;
        }

        private async Task WatchResourceAndCorrectUrlsAsync(CancellationToken cancellationToken)
        {
            // .NET Aspire always replaces URLs for resources that it fully manages in the dashboard.
            // People have been asking to allow us adding custom (non-localhost) URLs to resources but it is not yet possible with the default WithEndpoint methods.
            // https://github.com/dotnet/aspire/issues/5508
            // As a workaround, at each resource snapshot change, we make sure to inject the custom URL.
            // Any time it is removed due to a lifecycle event (resource started, stopped, etc.), it triggers the whole process again.
            // We also replace "localhost" with "127.0.0.1" during the process.
            await foreach (var evt in notificationService.WatchAsync(cancellationToken))
            {
                if (KnownResourceStates.TerminalStates.Contains(evt.Snapshot.State?.Text))
                {
                    await this.ReplaceLocalhostWithIpAddressIfNeededAsync(evt);
                    continue;
                }

                var reverseProxyEndpoint = evt.Resource.Annotations.OfType<ReverseProxyEndpointAnnotation>().FirstOrDefault();
                if (reverseProxyEndpoint == null)
                {
                    await this.ReplaceLocalhostWithIpAddressIfNeededAsync(evt);
                    continue;
                }

                if (IsReverseProxyEndpointAlreadyAdded(evt.Snapshot, reverseProxyEndpoint))
                {
                    await this.ReplaceLocalhostWithIpAddressIfNeededAsync(evt);
                    continue;
                }

                await notificationService.PublishUpdateAsync(evt.Resource, snapshot =>
                {
                    if (IsReverseProxyEndpointAlreadyAdded(snapshot, reverseProxyEndpoint))
                    {
                        // There was a race condition as the resource has already been updated by the time we execute this delegate
                        return snapshot;
                    }

                    return snapshot with
                    {
                        Urls =
                        [
                            new UrlSnapshot(EndpointNameHelper.GetReverseProxyEndpointName(), reverseProxyEndpoint.ReverseProxyUrl, IsInternal: false),
                            ..snapshot.Urls.Select(ReplaceLocalhostHostWithIpAddress)
                        ],
                    };
                });
            }
        }

        // .NET Aspire forces the use of "localhost" for URLs. To be consistent with the rest of the app,
        // we replace it with the corresponding IP address.
        private const string LocalhostPartialUrlToReplace = "://localhost:";
        private const string IpAddressPartialUrlReplacement = "://127.0.0.1:";

        private async Task ReplaceLocalhostWithIpAddressIfNeededAsync(ResourceEvent evt)
        {
            if (evt.Snapshot.Urls.Any(x => x.Url.Contains(LocalhostPartialUrlToReplace)))
            {
                await notificationService.PublishUpdateAsync(evt.Resource, snapshot => snapshot with
                {
                    Urls = [.. snapshot.Urls.Select(ReplaceLocalhostHostWithIpAddress)]
                });
            }
        }

        private static UrlSnapshot ReplaceLocalhostHostWithIpAddress(UrlSnapshot url) => url with
        {
            Url = url.Url.Replace(LocalhostPartialUrlToReplace, IpAddressPartialUrlReplacement)
        };

        private static bool IsReverseProxyEndpointAlreadyAdded(CustomResourceSnapshot snapshot, ReverseProxyEndpointAnnotation reverseProxyEndpoint)
        {
            return snapshot.Urls.Any(x => x.Url == reverseProxyEndpoint.ReverseProxyUrl);
        }

        public async ValueTask DisposeAsync()
        {
            await this._cts.CancelAsync();

            if (this._watchTask != null)
            {
                try
                {
                    await this._watchTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when the application is shutting down.
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while correcting correcting the URLs of resources.");
                }
            }

            this._cts.Dispose();
        }
    }
}