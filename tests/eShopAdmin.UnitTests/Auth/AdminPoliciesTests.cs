using System.Security.Claims;
using eShopAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace eShopAdmin.UnitTests.Auth;

[TestClass]
public class AdminPoliciesTests
{
    [TestMethod]
    [DataRow("RulesEditor", true)]
    [DataRow("RulesPublisher", true)]
    [DataRow(null, false)]
    public async Task AccessPolicyRequiresARulesRole(string? role, bool allowed)
        => Assert.AreEqual(allowed, (await Authorize(User(role), AdminPolicies.Access)).Succeeded);

    [TestMethod]
    public async Task AccessPolicyRejectsUnrelatedRole()
        => Assert.IsFalse((await Authorize(User("Customer"), AdminPolicies.Access)).Succeeded);

    [TestMethod]
    public async Task AccessPolicyRejectsAnonymousUser()
        => Assert.IsFalse((await Authorize(new ClaimsPrincipal(new ClaimsIdentity()), AdminPolicies.Access)).Succeeded);

    [TestMethod]
    public async Task PublishPolicyAllowsPublisher()
        => Assert.IsTrue((await Authorize(User("RulesPublisher"), AdminPolicies.Publish)).Succeeded);

    [TestMethod]
    public async Task PublishPolicyRejectsEditor()
        => Assert.IsFalse((await Authorize(User("RulesEditor"), AdminPolicies.Publish)).Succeeded);

    [TestMethod]
    public async Task FallbackPolicyAdmitsEditorsAndRejectsEveryoneElse()
    {
        var options = new AuthorizationOptions();
        AdminPolicies.Configure(options);
        Assert.IsNotNull(options.FallbackPolicy);

        Assert.IsTrue((await AuthorizePolicy(User("RulesEditor"), options.FallbackPolicy)).Succeeded);
        Assert.IsTrue((await AuthorizePolicy(User("RulesPublisher"), options.FallbackPolicy)).Succeeded);
        Assert.IsFalse((await AuthorizePolicy(User("Customer"), options.FallbackPolicy)).Succeeded);
        Assert.IsFalse((await AuthorizePolicy(new ClaimsPrincipal(new ClaimsIdentity()), options.FallbackPolicy)).Succeeded);

        // The fallback is the access policy, not the stricter publish one.
        Assert.IsFalse((await Authorize(User("RulesEditor"), AdminPolicies.Publish)).Succeeded);
    }

    private static ClaimsPrincipal User(string? role)
    {
        var claims = role is null ? [] : new[] { new Claim("role", role) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "name", "role"));
    }

    private static async Task<AuthorizationResult> Authorize(ClaimsPrincipal user, string policy)
    {
        using var provider = BuildProvider();
        return await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
    }

    private static async Task<AuthorizationResult> AuthorizePolicy(ClaimsPrincipal user, AuthorizationPolicy policy)
    {
        using var provider = BuildProvider();
        return await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy.Requirements);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AdminPolicies.Configure);
        return services.BuildServiceProvider();
    }
}
