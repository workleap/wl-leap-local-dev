namespace Leap.Cli.Platform;

internal sealed class AzureDevOpsAuthenticationHandler(AzureDevOpsAuthenticator authenticator) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await authenticator.AuthenticateAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}