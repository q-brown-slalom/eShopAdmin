using Microsoft.AspNetCore.Authorization;

namespace eShopAdmin.Auth;

public static class AdminPolicies
{
    public const string Access = "Admin.Access";
    public const string Publish = "Admin.Publish";
    public const string EditorRole = "RulesEditor";
    public const string PublisherRole = "RulesPublisher";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Access, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(EditorRole, PublisherRole));

        options.AddPolicy(Publish, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(PublisherRole));

        // A page without its own [Authorize] is denied by default.
        options.FallbackPolicy = options.GetPolicy(Access);
    }
}
