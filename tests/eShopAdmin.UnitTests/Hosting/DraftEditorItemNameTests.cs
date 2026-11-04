using System.Net;
using System.Text;

namespace eShopAdmin.UnitTests.Hosting;

[TestClass]
public class DraftEditorItemNameTests
{
    private const string DraftId = "5f0c1d52-8d58-4c3c-9d0e-6f0a8a1b2c3d";

    private const string RuleTypesJson = """
        [{"id":"PRC-001","name":"Price adjustments","affectsPrice":true,"targeting":"Targeted","default":{"entries":[]},"parameterSchema":{}}]
        """;

    private const string VersionsJson = $$"""
        [{"id":"{{DraftId}}","ruleTypeId":"PRC-001","scope":"default","versionNumber":3,
          "parameters":{"entries":[
            {"target":{"kind":"Item","id":7},"mode":"PercentOff","value":10},
            {"target":{"kind":"Item","id":99},"mode":"PercentOff","value":5},
            {"target":{"kind":"Brand","id":1},"mode":"PercentOff","value":5}]},
          "effectiveFrom":"2099-01-01T09:00:00+00:00","effectiveTo":null,"status":"Draft","createdBy":"bob",
          "createdAt":"2026-11-03T09:00:00+00:00","publishedBy":null,"publishedAt":null}]
        """;

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Rules(HttpRequestMessage request) =>
        Json(request.RequestUri!.AbsolutePath.EndsWith("/types", StringComparison.Ordinal) ? RuleTypesJson : VersionsJson);

    private static HttpResponseMessage Catalog(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        return path.EndsWith("catalogtypes", StringComparison.Ordinal) ? Json("""[{"id":1,"type":"Shoes"}]""")
            : path.EndsWith("catalogbrands", StringComparison.Ordinal) ? Json("""[{"id":1,"brand":"Acme"}]""")
            : Json("""[{"id":7,"name":"Trail <Runner>"}]""");
    }

    private static async Task<string> RenderAsync(AdminAppFactory factory)
    {
        using var client = factory.CreateAppClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/rules/PRC-001/drafts/{DraftId}");
        request.Headers.Add(AdminAppFactory.RoleHeader, "RulesEditor");
        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [TestMethod]
    public async Task KnownItemsShowTheirNameBesideTheIdAndUnknownOnesShowNone()
    {
        await using var factory = new AdminAppFactory(Rules, catalogApi: Catalog);

        var html = await RenderAsync(factory);

        StringAssert.Contains(html, "Item: <span class=\"item-name\">Trail &lt;Runner&gt;</span>");
        Assert.HasCount(1, System.Text.RegularExpressions.Regex.Matches(html, "item-name"));
        StringAssert.Contains(html, "value=\"99\"");
    }

    [TestMethod]
    public async Task CatalogBeingDownStillRendersTheEditorWithoutNames()
    {
        await using var factory = new AdminAppFactory(Rules, catalogApi: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var html = await RenderAsync(factory);

        StringAssert.Contains(html, "Catalog names are unavailable right now. Enter ids instead.");
        Assert.DoesNotContain("item-name", html);
        StringAssert.Contains(html, "name=\"Input.PriceAdjustment.Entries[0].Target.Id\"");
    }
}
