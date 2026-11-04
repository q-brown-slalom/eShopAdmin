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
    public void FallbackPolicyIsTheAccessPolicy()
    {
        var options = new AuthorizationOptions();
        AdminPolicies.Configure(options);

        Assert.IsNotNull(options.FallbackPolicy);
        CollectionAssert.AreEquivalent(
            options.GetPolicy(AdminPolicies.Access)!.Requirements.Select(r => r.GetType()).ToList(),
            options.FallbackPolicy.Requirements.Select(r => r.GetType()).ToList());
    }

    private static ClaimsPrincipal User(string? role)
    {
        var claims = role is null ? [] : new[] { new Claim("role", role) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "name", "role"));
    }

    private static async Task<AuthorizationResult> Authorize(ClaimsPrincipal user, string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AdminPolicies.Configure);
        using var provider = services.BuildServiceProvider();

        return await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
    }
}
