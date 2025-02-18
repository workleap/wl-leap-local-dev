using System.Net;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.Extensions.Http;

namespace Workleap.Leap.Testing;

internal static class HttpClientFactory
{
    public static HttpClient Create(Action<SocketsHttpHandler>? configureHandler = null, ILogger? logger = null)
    {
        var socketHandler = new SocketsHttpHandler
        {
            // https://www.meziantou.net/avoid-dns-issues-with-httpclient-in-dotnet.htm
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        configureHandler?.Invoke(socketHandler);

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        HttpMessageHandler handler = new PolicyHttpMessageHandler(retryPolicy) { InnerHandler = socketHandler };

        if (logger != null)
        {
            handler = new LoggingClientHandler(logger, socketHandler);
        }

        return new HttpClient(handler);
    }

    private sealed class LoggingClientHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public LoggingClientHandler(ILogger logger)
        {
            this._logger = logger;
        }

        public LoggingClientHandler(ILogger logger, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            this._logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            string? requestContent = null;
            if (request.Content?.Headers.ContentType?.MediaType == "application/json")
            {
                requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (requestContent == null)
            {
                this._logger.LogDebug("{Method} {Url} - {StatusCode}\n{Response}", request.Method, request.RequestUri, (int)response.StatusCode, responseContent);
            }
            else
            {
                this._logger.LogDebug("{Method} {Url} - {StatusCode}\n{RequestBody}\n---\n{Response}", request.Method, request.RequestUri, (int)response.StatusCode, requestContent, responseContent);
            }

            return response;
        }
    }
}