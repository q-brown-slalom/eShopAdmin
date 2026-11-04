using eShopAdmin.Auth;
using eShopAdmin.BusinessRules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace eShopAdmin.UnitTests.Auth;

[TestClass]
public class ReauthenticateMiddlewareTests
{
    [TestMethod]
    public async Task MiddlewareChallengesWithReturnUrl()
    {
        var (context, authentication) = CreateContext("/rules/ORD-001", "?x=1");
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        await authentication.Received(1).ChallengeAsync(
            context,
            OpenIdConnectDefaults.AuthenticationScheme,
            Arg.Is<AuthenticationProperties>(p => p.RedirectUri == "/rules/ORD-001?x=1"));
    }

    [TestMethod]
    public async Task MiddlewareKeepsPathBaseInReturnUrl()
    {
        var (context, authentication) = CreateContext("/rules", "", "/admin");
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        await authentication.Received(1).ChallengeAsync(
            context,
            OpenIdConnectDefaults.AuthenticationScheme,
            Arg.Is<AuthenticationProperties>(p => p.RedirectUri == "/admin/rules"));
    }

    [TestMethod]
    public async Task MiddlewareFindsTheExceptionBehindWrappers()
    {
        var (context, authentication) = CreateContext("/rules", "");
        var middleware = new ReauthenticateMiddleware(_ =>
            throw new InvalidOperationException("render failed", new ReauthenticationRequiredException()));

        await middleware.InvokeAsync(context);

        await authentication.Received(1).ChallengeAsync(context, OpenIdConnectDefaults.AuthenticationScheme, Arg.Any<AuthenticationProperties>());
    }

    [TestMethod]
    public async Task MiddlewareFallsBackToRootForNonLocalReturnUrl()
    {
        var (context, authentication) = CreateContext("//evil.example", "");
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        await authentication.Received(1).ChallengeAsync(
            context,
            OpenIdConnectDefaults.AuthenticationScheme,
            Arg.Is<AuthenticationProperties>(p => p.RedirectUri == "/"));
    }

    [TestMethod]
    public async Task FirstChallengeLeavesAShortLivedMarker()
    {
        var (context, _) = CreateContext("/rules", "");
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        StringAssert.StartsWith(setCookie, ".eShopAdmin.Reauth=1");
        StringAssert.Contains(setCookie, "max-age=120");
        StringAssert.Contains(setCookie, "httponly");
    }

    [TestMethod]
    public async Task SecondUnauthorizedInsideTheWindowIsNotChallengedAgain()
    {
        var (context, authentication) = CreateContext("/rules", "");
        context.Request.Headers.Cookie = ".eShopAdmin.Reauth=1";
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await Assert.ThrowsExactlyAsync<ReauthenticationRequiredException>(() => middleware.InvokeAsync(context));

        await authentication.DidNotReceiveWithAnyArgs().ChallengeAsync(default!, default, default);
        Assert.IsFalse(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [TestMethod]
    public async Task UnrelatedCookiesDoNotSuppressTheChallenge()
    {
        var (context, authentication) = CreateContext("/rules", "");
        context.Request.Headers.Cookie = "other=1";
        var middleware = new ReauthenticateMiddleware(_ => throw new ReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        await authentication.Received(1).ChallengeAsync(context, OpenIdConnectDefaults.AuthenticationScheme, Arg.Any<AuthenticationProperties>());
    }

    [TestMethod]
    public async Task MiddlewareLetsOtherExceptionsThrough()
    {
        var (context, authentication) = CreateContext("/rules", "");
        var middleware = new ReauthenticateMiddleware(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        await authentication.DidNotReceiveWithAnyArgs().ChallengeAsync(default!, default, default);
    }

    [TestMethod]
    public async Task MiddlewarePassesThroughWhenNothingIsThrown()
    {
        var (context, authentication) = CreateContext("/rules", "");
        var called = false;
        var middleware = new ReauthenticateMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.IsTrue(called);
        await authentication.DidNotReceiveWithAnyArgs().ChallengeAsync(default!, default, default);
    }

    private static (DefaultHttpContext Context, IAuthenticationService Authentication) CreateContext(string path, string query, string pathBase = "")
    {
        var authentication = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return (context, authentication);
    }
}
