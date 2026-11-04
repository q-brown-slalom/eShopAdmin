using System.Net;
using System.Text;
using eShopAdmin.BusinessRules;
using eShopAdmin.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eShopAdmin.UnitTests.Catalog;

[TestClass]
public class CatalogLookupClientTests
{
    [TestMethod]
    public async Task TypesAreReadWithTheApiVersion()
    {
        var (client, stub) = CreateClient(_ => Json(HttpStatusCode.OK, """[{"id":1,"type":"Mug"},{"id":2,"type":"Sheet"}]"""));

        var types = await client.GetTypesAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new CatalogOption(1, "Mug"), new CatalogOption(2, "Sheet") }, types!.ToArray());
        Assert.AreEqual("/api/catalog/catalogtypes", stub.Request!.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "api-version=1.0");
        Assert.IsNull(stub.Request.Headers.Authorization);
    }

    [TestMethod]
    public async Task BrandsAreRead()
    {
        var (client, stub) = CreateClient(_ => Json(HttpStatusCode.OK, """[{"id":5,"brand":"Daybird"}]"""));

        var brands = await client.GetBrandsAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new CatalogOption(5, "Daybird") }, brands!.ToArray());
        Assert.AreEqual("/api/catalog/catalogbrands", stub.Request!.RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task CatalogTypesReturnNullWhenCatalogFails()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.IsNull(await client.GetTypesAsync(CancellationToken.None));
        Assert.IsNull(await client.GetBrandsAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task LookupsReturnNullWhenCatalogIsUnreachableOrReturnsJunk()
    {
        var (unreachable, _) = CreateClient(_ => throw new HttpRequestException("connection refused"));
        var (junk, _) = CreateClient(_ => Json(HttpStatusCode.OK, "<html>not json</html>"));
        var (timedOut, _) = CreateClient(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        Assert.IsNull(await unreachable.GetTypesAsync(CancellationToken.None));
        Assert.IsNull(await junk.GetBrandsAsync(CancellationToken.None));
        Assert.IsNull(await timedOut.GetTypesAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task CallerCancellationIsNotSwallowed()
    {
        var (client, _) = CreateClient(_ => throw new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetTypesAsync(cts.Token));
    }

    [TestMethod]
    public async Task ItemNamesAreKeyedById()
    {
        var (client, stub) = CreateClient(_ => Json(HttpStatusCode.OK, """[{"id":3,"name":"Wanderer Mug","price":9.5},{"id":8,"name":"Trail Boots"}]"""));

        var names = await client.GetItemNamesAsync([3, 8], CancellationToken.None);

        Assert.AreEqual("Wanderer Mug", names[3]);
        Assert.AreEqual("Trail Boots", names[8]);
        Assert.AreEqual("/api/catalog/items/by", stub.Request!.RequestUri!.AbsolutePath);
        StringAssert.Contains(stub.Request.RequestUri.Query, "ids=3&ids=8");
        StringAssert.Contains(stub.Request.RequestUri.Query, "api-version=1.0");
    }

    [TestMethod]
    public async Task ItemNamesEmptyWhenCatalogFails()
    {
        var (failing, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (unreachable, _) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        Assert.IsEmpty(await failing.GetItemNamesAsync([1], CancellationToken.None));
        Assert.IsEmpty(await unreachable.GetItemNamesAsync([1], CancellationToken.None));
    }

    [TestMethod]
    public async Task ItemNamesWithoutIdsDoNotCallCatalog()
    {
        var (client, stub) = CreateClient(_ => Json(HttpStatusCode.OK, "[]"));

        var names = await client.GetItemNamesAsync([], CancellationToken.None);

        Assert.IsEmpty(names);
        Assert.IsNull(stub.Request);
    }

    [TestMethod]
    public async Task FailuresLogAWarningWithoutUserData()
    {
        var logger = Substitute.For<ILogger<CatalogLookupClient>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var (client, _) = CreateClient(_ => throw new HttpRequestException("connect to alice@example.com failed"), logger);

        await client.GetTypesAsync(CancellationToken.None);

        var calls = logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ToList();
        Assert.HasCount(1, calls);
        Assert.AreEqual(LogLevel.Warning, calls[0].GetArguments()[0]);
        Assert.DoesNotContain("alice@example.com", calls[0].GetArguments()[2]!.ToString()!);
    }

    [TestMethod]
    public async Task CatalogOutageDoesNotOpenTheCircuitForBusinessRules()
    {
        var catalogStub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var rulesStub = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var services = new ServiceCollection();
        services.AddLogging();

        // Mirrors what ServiceDefaults applies to every HTTP client: one standard pipeline, one circuit breaker.
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
        services.Configure<HttpStandardResilienceOptions>("-standard", o =>
        {
            o.Retry.Delay = TimeSpan.Zero;
            o.Retry.UseJitter = false;
        });
        services.AddBusinessRulesClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://businessrules-api.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => rulesStub);
        services.AddCatalogLookupClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://catalog-api.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => catalogStub);
        var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<CatalogLookupClient>();
        var rules = provider.GetRequiredService<BusinessRulesClient>();

        await rules.GetRuleTypesAsync(CancellationToken.None);
        for (var i = 0; i < 150; i++)
        {
            Assert.IsNull(await catalog.GetTypesAsync(CancellationToken.None));
        }

        Assert.IsEmpty(await rules.GetRuleTypesAsync(CancellationToken.None));
        Assert.AreEqual(150, catalogStub.Calls, "Catalog failures must not be retried.");
    }

    private static (CatalogLookupClient Client, StubHandler Stub) CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond, ILogger<CatalogLookupClient>? logger = null)
    {
        var stub = new StubHandler(respond);
        var services = new ServiceCollection();
        services.AddSingleton(logger ?? Substitute.For<ILogger<CatalogLookupClient>>());
        services.AddHttpClient<CatalogLookupClient>(client => client.BaseAddress = new Uri("http://catalog-api.test/"))
            .AddApiVersion(1.0)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        return (services.BuildServiceProvider().GetRequiredService<CatalogLookupClient>(), stub);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int calls;

        public HttpRequestMessage? Request { get; private set; }

        public int Calls => Volatile.Read(ref calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Interlocked.Increment(ref calls);
            return Task.FromResult(respond(request));
        }
    }
}
