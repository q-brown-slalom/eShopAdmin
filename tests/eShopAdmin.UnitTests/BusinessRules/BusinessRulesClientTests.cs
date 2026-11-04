using System.Net;
using System.Text;
using System.Text.Json;
using eShopAdmin.BusinessRules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eShopAdmin.UnitTests.BusinessRules;

[TestClass]
public class BusinessRulesClientTests
{
    private static readonly Guid VersionId = Guid.Parse("6f1e3c0a-5d2b-4c7e-9a41-0b8d2f6e7a13");

    [TestMethod]
    public async Task PublishSendsApiVersionAndOverrideReason()
    {
        var (client, stub) = CreateClient(Json(HttpStatusCode.OK, $$"""{"ruleVersionId":"{{VersionId}}","snapshotVersion":7}"""));

        var result = await client.PublishAsync(VersionId, "Spring sale", CancellationToken.None);

        Assert.AreEqual(new PublishResult.Published(7), result);
        Assert.AreEqual(HttpMethod.Post, stub.Request!.Method);
        Assert.AreEqual($"/api/rules/versions/{VersionId}/publish", stub.Request.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "api-version=1.0");
        using var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.AreEqual("Spring sale", body.RootElement.GetProperty("guardOverrideReason").GetString());
    }

    [TestMethod]
    public async Task PublishMapsV1ClientsActiveWithDetail()
    {
        var (client, _) = CreateClient(Problem(HttpStatusCode.Conflict, ProblemTypes.V1ClientsActive, "3 calls"));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.AreEqual(new PublishResult.V1ClientsActive("3 calls"), result);
    }

    [TestMethod]
    public async Task PublishMapsGuardUnavailable()
    {
        var (client, _) = CreateClient(Problem(HttpStatusCode.ServiceUnavailable, ProblemTypes.PublishGuardUnavailable, "usage unreadable"));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.IsInstanceOfType<PublishResult.GuardUnavailable>(result);
    }

    [TestMethod]
    public async Task PublishMapsInvalidOverrideReason()
    {
        var (client, _) = CreateClient(Problem(HttpStatusCode.BadRequest, ProblemTypes.InvalidGuardOverrideReason, "Too long"));

        var result = await client.PublishAsync(VersionId, new string('x', 501), CancellationToken.None);

        Assert.AreEqual(new PublishResult.InvalidOverrideReason("Too long"), result);
    }

    [TestMethod]
    public async Task PublishMapsConcurrentPublish()
    {
        var (client, _) = CreateClient(Problem(HttpStatusCode.Conflict, ProblemTypes.ConcurrentPublish, "Another publish"));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.IsInstanceOfType<PublishResult.ConcurrentPublish>(result);
    }

    [TestMethod]
    public async Task PublishMapsForbidden()
    {
        var (client, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.IsInstanceOfType<PublishResult.Forbidden>(result);
    }

    [TestMethod]
    public async Task PublishMapsNotFound()
    {
        var (client, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.IsInstanceOfType<PublishResult.NotFound>(result);
    }

    [TestMethod]
    public async Task PublishOfNonDraftMapsToConflict()
    {
        // A non-draft version comes back as a 409 with no problem type of ours (and a double submit hits this).
        var (client, _) = CreateClient(Json(HttpStatusCode.Conflict, """{"title":"Rule version is not a draft","status":409,"detail":"Rule version is Published; only drafts can be published."}"""));

        var result = await client.PublishAsync(VersionId, null, CancellationToken.None);

        Assert.AreEqual(new PublishResult.Conflict("Rule version is Published; only drafts can be published."), result);
    }

    [TestMethod]
    public async Task PublishOfUnexpectedStatusThrowsHttpRequestException()
    {
        var (client, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.PublishAsync(VersionId, null, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateDraftMapsValidationProblemErrors()
    {
        var (client, stub) = CreateClient(Json(HttpStatusCode.BadRequest, """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"parameters.minutes":["Must be between 1 and 60."],"effectiveTo":["Must be after effectiveFrom.","Required."]}}
            """));

        var result = await client.CreateDraftAsync(NewCreateRequest(), CancellationToken.None);

        var invalid = Assert.IsInstanceOfType<SaveDraftResult.Invalid>(result);
        Assert.HasCount(2, invalid.Errors);
        CollectionAssert.AreEqual(new[] { "Must be between 1 and 60." }, invalid.Errors["parameters.minutes"]);
        CollectionAssert.AreEqual(new[] { "Must be after effectiveFrom.", "Required." }, invalid.Errors["effectiveTo"]);
        Assert.AreEqual(HttpMethod.Post, stub.Request!.Method);
        Assert.AreEqual("/api/rules/versions", stub.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.AreEqual("ORD-001", body.RootElement.GetProperty("ruleTypeId").GetString());
        Assert.AreEqual("default", body.RootElement.GetProperty("scope").GetString());
        Assert.AreEqual(10, body.RootElement.GetProperty("parameters").GetProperty("minutes").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, body.RootElement.GetProperty("effectiveTo").ValueKind);
    }

    [TestMethod]
    public async Task CreateDraftMapsCreatedToSaved()
    {
        var (client, _) = CreateClient(Json(HttpStatusCode.Created, VersionJson("Draft")));

        var result = await client.CreateDraftAsync(NewCreateRequest(), CancellationToken.None);

        var saved = Assert.IsInstanceOfType<SaveDraftResult.Saved>(result);
        Assert.AreEqual(VersionId, saved.Version.Id);
        Assert.AreEqual(RuleVersionStatus.Draft, saved.Version.Status);
    }

    [TestMethod]
    public async Task UpdateDraftMapsOkToSavedAndSendsPut()
    {
        var (client, stub) = CreateClient(Json(HttpStatusCode.OK, VersionJson("Draft")));

        var result = await client.UpdateDraftAsync(VersionId, NewUpdateRequest(), CancellationToken.None);

        Assert.IsInstanceOfType<SaveDraftResult.Saved>(result);
        Assert.AreEqual(HttpMethod.Put, stub.Request!.Method);
        Assert.AreEqual($"/api/rules/versions/{VersionId}", stub.Request.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "api-version=1.0");
    }

    [TestMethod]
    public async Task UpdateDraftMapsConflictAndNotFound()
    {
        var (conflictClient, _) = CreateClient(Json(HttpStatusCode.Conflict, """{"title":"Rule version is not a draft","status":409,"detail":"Rule version is Published; only drafts can be edited."}"""));
        var (notFoundClient, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var conflict = await conflictClient.UpdateDraftAsync(VersionId, NewUpdateRequest(), CancellationToken.None);
        var notFound = await notFoundClient.UpdateDraftAsync(VersionId, NewUpdateRequest(), CancellationToken.None);

        Assert.AreEqual(new SaveDraftResult.Conflict("Rule version is Published; only drafts can be edited."), conflict);
        Assert.IsInstanceOfType<SaveDraftResult.NotFound>(notFound);
    }

    [TestMethod]
    public async Task UnauthorizedThrowsReauthenticationRequired()
    {
        var ct = CancellationToken.None;

        foreach (var call in new Func<BusinessRulesClient, Task>[]
        {
            c => c.GetRuleTypesAsync(ct),
            c => c.GetVersionsAsync("ORD-001", ct),
            c => c.CreateDraftAsync(NewCreateRequest(), ct),
            c => c.UpdateDraftAsync(VersionId, NewUpdateRequest(), ct),
            c => c.PublishAsync(VersionId, null, ct),
            c => c.GetAuditAsync(0, 10, ct),
        })
        {
            var (client, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            await Assert.ThrowsExactlyAsync<ReauthenticationRequiredException>(() => call(client));
        }
    }

    [TestMethod]
    public async Task GetVersionsParsesStringEnumsAndJsonParameters()
    {
        var (client, stub) = CreateClient(Json(HttpStatusCode.OK, $"[{VersionJson("Published")}]"));

        var versions = await client.GetVersionsAsync("ORD-001", CancellationToken.None);

        var version = versions.Single();
        Assert.AreEqual(RuleVersionStatus.Published, version.Status);
        Assert.AreEqual(10, version.Parameters.GetProperty("minutes").GetInt32());
        Assert.AreEqual(3, version.VersionNumber);
        Assert.AreEqual("alice-id", version.PublishedBy);
        Assert.IsNull(version.EffectiveTo);
        Assert.AreEqual("/api/rules/versions", stub.Request!.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "ruleTypeId=ORD-001");
        StringAssert.Contains(stub.Request.RequestUri.Query, "scope=default");
        StringAssert.Contains(stub.Request.RequestUri.Query, "api-version=1.0");
    }

    [TestMethod]
    public async Task GetRuleTypesParsesTargetingAndSchema()
    {
        var (client, _) = CreateClient(Json(HttpStatusCode.OK, """
            [{"id":"ORD-001","name":"Order hold","affectsPrice":false,"targeting":"Global","default":{"minutes":5},"parameterSchema":{"type":"object"}},
             {"id":"PRC-001","name":"Price","affectsPrice":true,"targeting":"Targeted","default":{},"parameterSchema":{}}]
            """));

        var types = await client.GetRuleTypesAsync(CancellationToken.None);

        Assert.HasCount(2, types);
        Assert.AreEqual(RuleTargeting.Global, types[0].Targeting);
        Assert.AreEqual(RuleTargeting.Targeted, types[1].Targeting);
        Assert.IsTrue(types[1].AffectsPrice);
        Assert.AreEqual(5, types[0].Default.GetProperty("minutes").GetInt32());
    }

    [TestMethod]
    public async Task GetAuditParsesPageAndActions()
    {
        var (client, stub) = CreateClient(Json(HttpStatusCode.OK, $$"""
            {"pageIndex":1,"pageSize":5,"count":11,"data":[
              {"id":42,"occurredAt":"2026-11-03T10:00:00+00:00","actorId":"alice-id","action":"GuardOverridden","ruleTypeId":"PRC-001","scope":"default",
               "ruleVersionId":"{{VersionId}}","beforeJson":null,"afterJson":null,"reason":"Spring sale"}]}
            """));

        var page = await client.GetAuditAsync(1, 5, CancellationToken.None);

        Assert.AreEqual(1, page.PageIndex);
        Assert.AreEqual(5, page.PageSize);
        Assert.AreEqual(11L, page.Count);
        var entry = page.Data.Single();
        Assert.AreEqual(AuditAction.GuardOverridden, entry.Action);
        Assert.AreEqual("Spring sale", entry.Reason);
        Assert.AreEqual("/api/rules/audit", stub.Request!.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "pageIndex=1");
        StringAssert.Contains(stub.Request.RequestUri.Query, "pageSize=5");
    }

    [TestMethod]
    public async Task PublishLogsSnapshotWithoutUserData()
    {
        var logger = Substitute.For<ILogger<BusinessRulesClient>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var (client, _) = CreateClient(Json(HttpStatusCode.OK, $$"""{"ruleVersionId":"{{VersionId}}","snapshotVersion":7}"""), logger);

        await client.PublishAsync(VersionId, "Spring sale", CancellationToken.None);

        var logged = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(c => c.GetArguments()[2]!.ToString()!)
            .ToList();
        Assert.HasCount(1, logged);
        StringAssert.Contains(logged[0], "published as snapshot 7");
        Assert.DoesNotContain("Spring sale", logged[0]);
    }

    private static (BusinessRulesClient Client, StubHandler Stub) CreateClient(HttpResponseMessage response, ILogger<BusinessRulesClient>? logger = null)
    {
        var stub = new StubHandler(response);
        var services = new ServiceCollection();
        services.AddSingleton(logger ?? Substitute.For<ILogger<BusinessRulesClient>>());
        services.AddHttpClient<BusinessRulesClient>(client => client.BaseAddress = new Uri("http://businessrules-api.test/"))
            .AddApiVersion(1.0)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        return (services.BuildServiceProvider().GetRequiredService<BusinessRulesClient>(), stub);
    }

    private static CreateDraftRequest NewCreateRequest()
        => new("ORD-001", "default", JsonSerializer.SerializeToElement(new { minutes = 10 }), DateTimeOffset.Parse("2026-12-01T00:00:00Z"), null);

    private static UpdateDraftRequest NewUpdateRequest()
        => new(JsonSerializer.SerializeToElement(new { minutes = 10 }), DateTimeOffset.Parse("2026-12-01T00:00:00Z"), null);

    private static string VersionJson(string status)
        => $$"""
            {"id":"{{VersionId}}","ruleTypeId":"ORD-001","scope":"default","versionNumber":3,"parameters":{"minutes":10},
             "effectiveFrom":"2026-12-01T00:00:00+00:00","effectiveTo":null,"status":"{{status}}","createdBy":"bob-id",
             "createdAt":"2026-11-03T09:00:00+00:00","publishedBy":"alice-id","publishedAt":"2026-11-03T10:00:00+00:00"}
            """;

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Problem(HttpStatusCode status, string type, string detail)
        => new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { type, title = "Problem", status = (int)status, detail }),
                Encoding.UTF8,
                "application/problem+json"),
        };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
