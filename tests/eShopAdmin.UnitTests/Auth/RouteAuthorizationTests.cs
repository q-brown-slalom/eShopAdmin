using System.Reflection;
using eShopAdmin.Auth;
using eShopAdmin.Components.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace eShopAdmin.UnitTests.Auth;

[TestClass]
public class RouteAuthorizationTests
{
    [TestMethod]
    public void PublishPageRequiresThePublishPolicy()
    {
        var page = typeof(Home).Assembly.GetType("eShopAdmin.Components.Pages.Rules.PublishVersion");
        Assert.IsNotNull(page, "The publish page must exist.");

        Assert.AreEqual(
            "/rules/{RuleTypeId}/versions/{Id:guid}/publish",
            page.GetCustomAttributes<RouteAttribute>().Single().Template);
        Assert.AreEqual(AdminPolicies.Publish, page.GetCustomAttributes<AuthorizeAttribute>().Single().Policy);
    }

    [TestMethod]
    public void EveryRoutablePageRequiresAuthorizationExceptAccessDenied()
    {
        var routable = typeof(Home).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any())
            .ToList();

        CollectionAssert.Contains(routable, typeof(Home));
        CollectionAssert.Contains(routable, typeof(AccessDenied));

        var options = new AuthorizationOptions();
        AdminPolicies.Configure(options);
        Assert.IsNotNull(options.FallbackPolicy, "Pages without [Authorize] rely on the fallback policy.");

        foreach (var page in routable)
        {
            var allowsAnonymous = page.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            if (page == typeof(AccessDenied))
            {
                Assert.IsTrue(allowsAnonymous, "AccessDenied must be reachable without a rules role.");
            }
            else
            {
                Assert.IsFalse(allowsAnonymous, $"{page.Name} must not allow anonymous access.");
            }
        }
    }
}
