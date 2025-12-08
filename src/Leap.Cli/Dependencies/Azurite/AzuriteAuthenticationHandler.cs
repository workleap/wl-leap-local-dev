using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed class AzuriteAuthenticationHandler : DelegatingHandler
{
    private readonly string _accessToken;

    public AzuriteAuthenticationHandler()
    {
        var utcNow = DateTime.UtcNow;

        // Inspired from Azurite's HTTPS + OAuth tests:
        // https://github.com/Azure/Azurite/blob/v3.31.0/tests/blob/oauth.test.ts#L57-L65
        //
        // > Azurite performs basic authentication, like validating the incoming bearer token, checking the issuer,
        // > audience, and expiry. Azurite doesn't check the token signature or permissions.
        // https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite#oauth-configuration
        //
        // Thanks to this, we can provide a randomly signed, valid token with a dummy issuer and valid audience.
        // It replaces the need to use a real Azure identity (DefaultAzureCredential and other TokenCredential primitives).
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            NotBefore = utcNow,
            IssuedAt = utcNow,
            Expires = utcNow.AddYears(1),
            Issuer = "https://sts.windows.net/00000000-0000-0000-0000-000000000000/", // Usually an Azure AD tenant ID
            Audience = "https://storage.azure.com",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                SecurityAlgorithms.HmacSha256),
        };

        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        var token = jwtSecurityTokenHandler.CreateToken(tokenDescriptor);
        this._accessToken = jwtSecurityTokenHandler.WriteToken(token);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this._accessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}