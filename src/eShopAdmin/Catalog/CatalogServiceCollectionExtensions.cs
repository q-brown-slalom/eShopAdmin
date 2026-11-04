using Asp.Versioning;
using Microsoft.Extensions.Http.Resilience;

namespace eShopAdmin.Catalog;

public static class CatalogServiceCollectionExtensions
{
    /// <summary>
    /// Registers the client with no resilience handler of its own and a 3 second overall timeout. ServiceDefaults' standard
    /// handler is one shared pipeline (one circuit breaker) for every client, so a Catalog outage would open the circuit for
    /// BusinessRules.API too, and its retries would hold a page up for much longer than Catalog names are worth.
    /// </summary>
    // RemoveAllResilienceHandlers is the only way to opt one client out of the defaults, and it is marked experimental.
#pragma warning disable EXTEXP0001
    public static IHttpClientBuilder AddCatalogLookupClient(this IServiceCollection services) =>
        services.AddHttpClient<CatalogLookupClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://catalog-api");
                client.Timeout = TimeSpan.FromSeconds(3);
            })
            .AddApiVersion(1.0)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
}
