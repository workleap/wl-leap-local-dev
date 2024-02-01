using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed class AzuriteAuthenticationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Authenticate(request, AzuriteConstants.AccountName, AzuriteConstants.AccountKey);
        return await base.SendAsync(request, cancellationToken);
    }

    // https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-shared-key
    private static void Authenticate(HttpRequestMessage request, string accountName, string accountKey)
    {
        var message = IsTableRequest(request, accountName)
            ? BuildStringToSignForTableRequest(request, accountName)
            : BuildStringToSignForBlobOrQueueRequest(request, accountName);

        var messageBytes = Encoding.UTF8.GetBytes(message);

        var signatureBytes = HMACSHA256.HashData(key: Convert.FromBase64String(accountKey), source: messageBytes);
        var signature = Convert.ToBase64String(signatureBytes);

        request.Headers.Authorization = new AuthenticationHeaderValue("SharedKey", accountName + ":" + signature);
    }

    private static bool IsTableRequest(HttpRequestMessage request, string accountName)
    {
        return request.RequestUri!.AbsolutePath.StartsWith("/" + accountName + "/Tables", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStringToSignForBlobOrQueueRequest(HttpRequestMessage request, string accountName)
    {
        var method = request.Method;
        var contentEncoding = request.Content?.Headers.ContentEncoding?.ToString() ?? string.Empty;
        var contentLanguage = request.Content?.Headers.ContentLanguage?.ToString() ?? string.Empty;
        var contentLength = request.Content?.Headers.ContentLength?.ToString() ?? string.Empty;
        var contentMd5 = request.Content?.Headers.ContentMD5?.ToString() ?? string.Empty;
        var contentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;
        var date = request.Headers.Date?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        var ifModifiedSince = request.Headers.IfModifiedSince?.ToString() ?? string.Empty;
        var ifMatch = request.Headers.IfMatch?.ToString() ?? string.Empty;
        var ifNoneMatch = request.Headers.IfNoneMatch?.ToString() ?? string.Empty;
        var ifUnmodifiedSince = request.Headers.IfUnmodifiedSince?.ToString() ?? string.Empty;
        var canonicalizedMsHeaders = string.Empty;
        var range = request.Headers.Range?.ToString() ?? string.Empty;

        var sb = new StringBuilder();

        sb.Append(method).Append('\n');
        sb.Append(contentEncoding).Append('\n');
        sb.Append(contentLanguage).Append('\n');
        sb.Append(contentLength).Append('\n');
        sb.Append(contentMd5).Append('\n');
        sb.Append(contentType).Append('\n');
        sb.Append(date).Append('\n');
        sb.Append(ifModifiedSince).Append('\n');
        sb.Append(ifMatch).Append('\n');
        sb.Append(ifNoneMatch).Append('\n');
        sb.Append(ifUnmodifiedSince).Append('\n');
        sb.Append(range).Append('\n');
        sb.Append(canonicalizedMsHeaders);

        AppendCanonicalizedResource(sb, request.RequestUri!, accountName);

        return sb.ToString();
    }

    private static string BuildStringToSignForTableRequest(HttpRequestMessage request, string accountName)
    {
        var method = request.Method;
        var contentMd5 = request.Content?.Headers.ContentMD5?.ToString() ?? string.Empty;
        var contentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;
        var date = request.Headers.Date?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

        var sb = new StringBuilder();

        sb.Append(method).Append('\n');
        sb.Append(contentMd5).Append('\n');
        sb.Append(contentType).Append('\n');
        sb.Append(date).Append('\n');

        AppendCanonicalizedResource(sb, request.RequestUri!, accountName);

        return sb.ToString();
    }

    private static void AppendCanonicalizedResource(StringBuilder sb, Uri resource, string accountName)
    {
        sb.Append('/');
        sb.Append(accountName);
        sb.Append(resource.AbsolutePath.Length > 0 ? resource.AbsolutePath : "/");

        var parameters = HttpUtility.ParseQueryString(resource.Query);

        if (parameters.Count > 0)
        {
            foreach (var key in parameters.AllKeys.OrderBy(key => key, StringComparer.Ordinal))
            {
                sb.Append('\n').Append(key!.ToLowerInvariant()).Append(':').Append(parameters[key]);
            }
        }
    }
}