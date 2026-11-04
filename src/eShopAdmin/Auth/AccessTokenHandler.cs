using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace eShopAdmin.Auth;

/// <summary>Forwards the signed-in user's saved access token to BusinessRules.API.</summary>
public sealed class AccessTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext is { } context
            && await context.GetTokenAsync("access_token") is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
