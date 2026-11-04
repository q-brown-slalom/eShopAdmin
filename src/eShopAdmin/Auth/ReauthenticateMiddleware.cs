using eShopAdmin.BusinessRules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace eShopAdmin.Auth;

/// <summary>Turns a 401 from BusinessRules.API into a new sign-in that returns to the same page.</summary>
public sealed class ReauthenticateMiddleware(RequestDelegate next)
{
    internal const string AttemptCookie = ".eShopAdmin.Reauth";

    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        // Sign in again at most once per window: a second 401 right after means a new token didn't help (wrong issuer,
        // audience or scope), so the error is left to surface instead of looping through Identity.
        catch (Exception ex) when (!context.Response.HasStarted
            && IsReauthenticationRequired(ex)
            && !context.Request.Cookies.ContainsKey(AttemptCookie))
        {
            context.Response.Cookies.Append(AttemptCookie, "1", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = AttemptWindow,
                IsEssential = true,
            });

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
