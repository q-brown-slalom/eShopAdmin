using Asp.Versioning;
using eShopAdmin.Auth;
using Microsoft.Extensions.Http.Resilience;

namespace eShopAdmin.BusinessRules;

public static class BusinessRulesServiceCollectionExtensions
{
    public static IHttpClientBuilder AddBusinessRulesClient(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<AccessTokenHandler>();

        // Publish and save are not idempotent: a retry after the server committed would duplicate a draft or
        // turn a successful publish into a "not a draft" conflict. ServiceDefaults adds the standard handler through
        // ConfigureHttpClientDefaults, which registers its options under the name "-standard" (an empty client name),
        // not "<client>-standard". It is shared by every default pipeline, and this is the only factory client here.
        services.Configure<HttpStandardResilienceOptions>(
            "-standard",
            options => options.Retry.DisableForUnsafeHttpMethods());

        return services.AddHttpClient<BusinessRulesClient>(client => client.BaseAddress = new Uri("https+http://businessrules-api"))
            .AddApiVersion(1.0)
            .AddHttpMessageHandler<AccessTokenHandler>();
    }
}
