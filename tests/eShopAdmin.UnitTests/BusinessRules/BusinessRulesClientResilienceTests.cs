using System.Net;
using eShopAdmin.BusinessRules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace eShopAdmin.UnitTests.BusinessRules;

[TestClass]
public class BusinessRulesClientResilienceTests
{
    private static readonly Guid VersionId = Guid.Parse("6f1e3c0a-5d2b-4c7e-9a41-0b8d2f6e7a13");

    [TestMethod]
    public async Task PublishIsNotRetriedOn503()
    {
        var (client, stub) = CreateClient(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.PublishAsync(VersionId, null, CancellationToken.None));

        Assert.AreEqual(1, stub.Count(HttpMethod.Post));
    }

    [TestMethod]
    public async Task CreateDraftIsNotRetriedOn500()
    {
        var (client, stub) = CreateClient(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateDraftAsync(
            new CreateDraftRequest("ORD-001", "default", default, DateTimeOffset.UtcNow, null), CancellationToken.None));

        Assert.AreEqual(1, stub.Count(HttpMethod.Post));
    }

    [TestMethod]
    public async Task UpdateDraftIsNotRetriedOn500()
    {
        var (client, stub) = CreateClient(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateDraftAsync(
            VersionId, new UpdateDraftRequest(default, DateTimeOffset.UtcNow, null), CancellationToken.None));

        Assert.AreEqual(1, stub.Count(HttpMethod.Put));
    }

    [TestMethod]
    public async Task SafeReadsAreStillRetried()
    {
        var (client, stub) = CreateClient(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetRuleTypesAsync(CancellationToken.None));

        Assert.IsGreaterThan(1, stub.Count(HttpMethod.Get));
    }

    private static (BusinessRulesClient Client, CountingHandler Stub) CreateClient(HttpStatusCode status)
    {
        var stub = new CountingHandler(status);
        var services = new ServiceCollection();
        services.AddLogging();

        // Mirrors what ServiceDefaults applies to every HTTP client.
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        services.AddBusinessRulesClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://businessrules-api.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => stub);

        // Keep retries fast; only the delay differs from the production pipeline.
        services.Configure<HttpStandardResilienceOptions>("-standard", o =>
        {
            o.Retry.Delay = TimeSpan.Zero;
            o.Retry.UseJitter = false;
        });

        return (services.BuildServiceProvider().GetRequiredService<BusinessRulesClient>(), stub);
    }

    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        private readonly List<HttpMethod> methods = [];

        public int Count(HttpMethod method)
        {
            lock (methods)
            {
                return methods.Count(m => m == method);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (methods)
            {
                methods.Add(request.Method);
            }

            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
