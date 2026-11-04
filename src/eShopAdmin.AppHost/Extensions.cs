namespace eShopAdmin.AppHost;

internal static class Extensions
{
    /// <summary>
    /// Declares the eShop services that eShopAdmin talks to as external services (their URLs are
    /// parameters, so neither repo builds the other) and wires up the admin web app.
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddEShopAdmin(this IDistributedApplicationBuilder builder, string launchProfileName)
    {
        var identityUrl = builder.AddParameter("identity-url", "https://localhost:5243");
        var identity = builder.AddExternalService("identity-api", identityUrl);
        var businessRules = builder.AddExternalService("businessrules-api", builder.AddParameter("businessrules-url", "http://localhost:5230"));
        var catalog = builder.AddExternalService("catalog-api", builder.AddParameter("catalog-url", "http://localhost:5222"));
        var admin = builder.AddProject<Projects.eShopAdmin>("eshopadmin", launchProfileName)
            .WithExternalHttpEndpoints()
            .WithReference(identity).WithReference(businessRules).WithReference(catalog)
            .WithEnvironment("IdentityUrl", identityUrl);
        admin.WithEnvironment("CallBackUrl", admin.GetEndpoint(launchProfileName));

        return admin;
    }
}
