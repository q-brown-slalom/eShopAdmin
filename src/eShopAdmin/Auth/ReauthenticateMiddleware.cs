using eShopAdmin.BusinessRules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace eShopAdmin.Auth;

/// <summary>Turns a 401 from BusinessRules.API into a new sign-in that returns to the same page.</summary>
public sealed class ReauthenticateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && IsReauthenticationRequired(ex))
        {
            var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            await context.ChallengeAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = AuthenticationExtensions.IsLocalUrl(returnUrl) ? returnUrl : "/" });
        }
    }

    // Component rendering can wrap exceptions thrown by a page.
    private static bool IsReauthenticationRequired(Exception? ex)
        => ex switch
        {
            null => false,
            ReauthenticationRequiredException => true,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsReauthenticationRequired),
            _ => IsReauthenticationRequired(ex.InnerException),
        };
}
