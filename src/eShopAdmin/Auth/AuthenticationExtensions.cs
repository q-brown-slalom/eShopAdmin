using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace eShopAdmin.Auth;

public static class AuthenticationExtensions
{
    public const string LoginPath = "/authentication/login";
    public const string LogoutPath = "/authentication/logout";

    public static IHostApplicationBuilder AddAdminAuthentication(this IHostApplicationBuilder builder)
    {
        var identityUrl = builder.Configuration["IdentityUrl"]
            ?? throw new InvalidOperationException("Configuration value 'IdentityUrl' is required.");

        // The AppHost's http launch profile (used by Playwright in CI) serves the app over plain HTTP.
        var callbackUrl = builder.Configuration["CallBackUrl"];
        var cookieSecurePolicy = callbackUrl is not null && callbackUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        builder.Services.AddAuthorization(AdminPolicies.Configure);

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.SlidingExpiration = false;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookieSecurePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.AccessDeniedPath = "/access-denied";
            })
            .AddOpenIdConnect(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = identityUrl;
                options.ClientId = "eshopadmin";
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                options.Scope.Clear();
                foreach (var scope in new[] { "openid", "profile", "roles", "businessrules.admin" })
                {
                    options.Scope.Add(scope);
                }

                options.ClaimActions.MapJsonKey("role", "role");
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";

                options.Events.OnTokenValidated = context =>
                {
                    if (!int.TryParse(context.TokenEndpointResponse?.ExpiresIn, out var expiresIn))
                    {
                        context.Fail("The token endpoint response did not include expires_in.");
                        return Task.CompletedTask;
                    }

                    context.Properties!.ExpiresUtc = SessionLifetime.ExpiresAt(TimeProvider.System.GetUtcNow(), expiresIn);
                    context.Properties.IsPersistent = false;
                    return Task.CompletedTask;
                };
            });

        return builder;
    }

    public static IEndpointRouteBuilder MapAdminAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LoginPath, (string? returnUrl) =>
            TypedResults.Challenge(
                new AuthenticationProperties { RedirectUri = IsLocalUrl(returnUrl) ? returnUrl : "/" },
                [OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        endpoints.MapPost(LogoutPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            return TypedResults.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        })
            .AllowAnonymous();

        return endpoints;
    }

    internal static bool IsLocalUrl([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? url)
        => !string.IsNullOrEmpty(url)
            && url[0] == '/'
            && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
            && !url.Any(char.IsControl);
}
