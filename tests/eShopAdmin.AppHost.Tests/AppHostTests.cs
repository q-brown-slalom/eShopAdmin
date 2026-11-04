using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using eShopAdmin.AppHost;

namespace eShopAdmin.AppHost.Tests;

[TestClass]
public class AppHostTests
{
    [TestMethod]
    public void AdminReferencesExternalServicesByParameter()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(AppHostTests).Assembly.FullName,
            DisableDashboard = true,
            ProjectDirectory = Path.Combine(FindRepositoryRoot(), "src", "eShopAdmin.AppHost")
        });

        builder.AddEShopAdmin("https");

        foreach (var name in new[] { "identity-api", "businessrules-api", "catalog-api" })
        {
            Assert.IsTrue(
                builder.Resources.OfType<ExternalServiceResource>().Any(r => r.Name == name),
                $"{name} should be an external service.");
        }

        foreach (var name in new[] { "identity-url", "businessrules-url", "catalog-url" })
        {
            Assert.IsTrue(
                builder.Resources.OfType<ParameterResource>().Any(r => r.Name == name),
                $"{name} should be a parameter.");
        }

        var admin = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "eshopadmin");
        var referenced = admin.Annotations.OfType<ResourceRelationshipAnnotation>()
            .Where(a => a.Type == "Reference")
            .Select(a => a.Resource.Name)
            .ToList();

        CollectionAssert.IsSubsetOf(new[] { "identity-api", "businessrules-api", "catalog-api" }, referenced);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eShopAdmin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("eShopAdmin.slnx was not found.");
    }
}
