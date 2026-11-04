using System.Net;
using System.Text;

namespace eShopAdmin.UnitTests.Hosting;

[TestClass]
public class RuleHistoryPageTests
{
    private const string RuleTypesJson = """
        [{"id":"ORD-001","name":"Order grace period","affectsPrice":false,"targeting":"Global","default":{"minutes":1},"parameterSchema":{}}]
        """;

    private static HttpResponseMessage Rules(HttpRequestMessage request) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            request.RequestUri!.AbsolutePath.EndsWith("/types", StringComparison.Ordinal) ? RuleTypesJson : "[]",
            Encoding.UTF8,
            "application/json"),
    };

    private static async Task<string> GetHistoryAsync(string query)
    {
        await using var factory = new AdminAppFactory(Rules);
        using var client = factory.CreateAppClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rules/ORD-001" + query);
        request.Headers.Add(AdminAppFactory.RoleHeader, "RulesEditor");
        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [TestMethod]
    public async Task FixedCodesRenderTheServerCopyInTheStatusRegion()
    {
        StringAssert.Contains(await GetHistoryAsync("?status=draft-saved&v=3"), "<div role=\"status\" class=\"status-message\">Draft v3 saved.</div>");
        StringAssert.Contains(await GetHistoryAsync("?status=published&snapshot=12"), "Published. Rules snapshot 12.");
    }

    [TestMethod]
    [DataRow("?status=Published.%20Your%20account%20is%20compromised")]
    [DataRow("?status=draft-saved")]
    [DataRow("?status=draft-saved&v=abc")]
    [DataRow("?status=published&snapshot=-1")]
    [DataRow("?v=3&snapshot=4")]
    public async Task CraftedLinksShowNoBanner(string query)
    {
        var html = await GetHistoryAsync(query);

        StringAssert.Contains(html, "<div role=\"status\" class=\"status-message\"></div>");
    }

    [TestMethod]
    public async Task CreatedAndPublishedColumnsAreLabelledUtc()
    {
        await using var factory = new AdminAppFactory(request => request.RequestUri!.AbsolutePath.EndsWith("/types", StringComparison.Ordinal)
            ? Rules(request)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"id":"5f0c1d52-8d58-4c3c-9d0e-6f0a8a1b2c3d","ruleTypeId":"ORD-001","scope":"default","versionNumber":1,"parameters":{"minutes":5},"effectiveFrom":"2026-11-03T09:00:00+00:00","effectiveTo":null,"status":"Published","createdBy":"bob","createdAt":"2026-11-03T09:00:00+00:00","publishedBy":"al","publishedAt":"2026-11-03T10:00:00+00:00"}]""",
                    Encoding.UTF8,
                    "application/json"),
            });
        using var client = factory.CreateAppClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rules/ORD-001");
        request.Headers.Add(AdminAppFactory.RoleHeader, "RulesEditor");
        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        StringAssert.Contains(html, "<th scope=\"col\">Created (UTC)</th>");
        StringAssert.Contains(html, "<th scope=\"col\">Published (UTC)</th>");
    }
}
