using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using eShopAdmin.BusinessRules;
using eShopAdmin.Catalog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace eShopAdmin.UnitTests.Hosting;

/// <summary>
/// The whole app on a TestServer. BusinessRules.API is a fake, sign-in is a header-driven test scheme, and the real
/// OpenID Connect handler still runs its challenge against a fixed configuration (no network).
/// </summary>
public sealed class AdminAppFactory(
    Func<HttpRequestMessage, HttpResponseMessage> rulesApi,
    string environment = "Production",
    Func<HttpRequestMessage, HttpResponseMessage>? catalogApi = null) : WebApplicationFactory<Program>
{
    public const string RoleHeader = "X-Test-Role";
    public const string AuthorizeEndpoint = "https://identity.test/connect/authorize";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("IdentityUrl", "https://identity.test");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<BusinessRulesClient>();
            services.AddTransient(_ => new BusinessRulesClient(
                new HttpClient(new FakeHandler(rulesApi)) { BaseAddress = new Uri("https://rules.test/") },
                NullLogger<BusinessRulesClient>.Instance));

            if (catalogApi is not null)
            {
                services.RemoveAll<CatalogLookupClient>();
                services.AddTransient(_ => new CatalogLookupClient(
                    new HttpClient(new FakeHandler(catalogApi)) { BaseAddress = new Uri("https://catalog.test/") },
                    NullLogger<CatalogLookupClient>.Instance));
            }

            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = HeaderAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = HeaderAuthHandler.SchemeName;
            });

            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://identity.test",
                    AuthorizationEndpoint = AuthorizeEndpoint,
                    TokenEndpoint = "https://identity.test/connect/token",
                };
                o.Configuration = configuration;
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    public HttpClient CreateAppClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    /// <summary>The return URL the app stored in the sign-in state of an OpenID Connect redirect.</summary>
    public string? ReturnUrlOf(Uri authorizeRedirect)
    {
        var state = System.Web.HttpUtility.ParseQueryString(authorizeRedirect.Query)["state"];
        Assert.IsNotNull(state, "The authorize redirect must carry a state value.");

        var options = Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        return options.StateDataFormat.Unprotect(state)?.RedirectUri;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim("name", "Test User"), new Claim("role", role.ToString())], SchemeName, "name", "role");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return Task.CompletedTask;
        }
    }
}
