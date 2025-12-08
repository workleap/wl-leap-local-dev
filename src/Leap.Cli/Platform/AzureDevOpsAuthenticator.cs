using Leap.Cli.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Leap.Cli.Platform;

internal sealed class AzureDevOpsAuthenticator
{
    private static readonly string[] Scopes = [Constants.AzureDevOps.AzureDevOpsScope];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureDevOpsAuthenticator> _logger;
    private readonly Lazy<Task<IPublicClientApplication>> _lazyPublicClientApp;

    public AzureDevOpsAuthenticator(IHttpClientFactory httpClientFactory, ILogger<AzureDevOpsAuthenticator> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;

        this._lazyPublicClientApp = new Lazy<Task<IPublicClientApplication>>(this.CreatePublicClientApplicationAsync);
    }

    private async Task<IPublicClientApplication> CreatePublicClientApplicationAsync()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(Constants.Msal.Cache.CacheFileName, Constants.Msal.Cache.CacheDirectoryPath)
            .WithLinuxKeyring(
                Constants.Msal.Cache.LinuxKeyRingSchema,
                Constants.Msal.Cache.LinuxKeyRingCollection,
                Constants.Msal.Cache.LinuxKeyRingLabel,
                Constants.Msal.Cache.LinuxKeyRingAttr1,
                Constants.Msal.Cache.LinuxKeyRingAttr2)
            .WithMacKeyChain(
                Constants.Msal.Cache.MacKeyChainServiceName,
                Constants.Msal.Cache.MacKeyChainAccountName)
            .Build();

        var app = PublicClientApplicationBuilder.Create(Constants.Msal.ClientId)
            .WithHttpClientFactory(new MsalHttpClientFactory(this._httpClientFactory))
            .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
            .WithTenantId(Constants.Msal.WorkleapTenantId)
            .WithDefaultRedirectUri()
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(app.UserTokenCache);

        return app;
    }

    public async Task AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var result = await this.GetAuthenticationResultAsync(cancellationToken);

        request.Headers.Remove("Authorization");
        request.Headers.Add("Authorization", result.CreateAuthorizationHeader());
    }

    private async Task<AuthenticationResult> GetAuthenticationResultAsync(CancellationToken cancellationToken)
    {
        var app = await this._lazyPublicClientApp.Value;

        AuthenticationResult result;

        try
        {
            var accounts = await app.GetAccountsAsync();
            var existingAccount = accounts.FirstOrDefault();

            if (existingAccount != null)
            {
                this._logger.LogTrace("Attempting to authenticate silently against Azure DevOps using the account {Account}...", existingAccount.Username);
            }

            result = await app.AcquireTokenSilent(Scopes, existingAccount).ExecuteAsync(cancellationToken);

            this._logger.LogTrace("Silent authentication succeeded");
        }
        catch (MsalUiRequiredException msalUiEx)
        {
            this._logger.LogTrace("Attempting to authenticate interactively against Azure DevOps...");

            try
            {
                result = await app.AcquireTokenWithDeviceCode(Scopes, this.WriteDeviceCodeInstructionsAsync)
                    .WithClaims(msalUiEx.Claims)
                    .ExecuteAsync(cancellationToken);
            }
            catch (MsalServiceException msalSvcEx)
            {
                // https://datatracker.ietf.org/doc/html/rfc8628#section-3.5
                var isDeviceCodeExpired = msalSvcEx.ErrorCode == "expired_token";
                if (isDeviceCodeExpired)
                {
                    throw new LeapException("The device code has expired. Please try again.");
                }

                throw new InvalidOperationException("An error occurred while acquiring a token with device code when authenticating against Azure DevOps.", msalSvcEx);
            }

            this._logger.LogTrace("Interactive authentication succeeded");
        }

        return result;
    }

    private Task WriteDeviceCodeInstructionsAsync(DeviceCodeResult deviceCodeResult)
    {
        this._logger.LogWarning("{Instructions}", deviceCodeResult.Message);
        return Task.CompletedTask;
    }

    private sealed class MsalHttpClientFactory(IHttpClientFactory httpClientFactory) : IMsalHttpClientFactory
    {
        public HttpClient GetHttpClient()
        {
            return httpClientFactory.CreateClient();
        }
    }
}