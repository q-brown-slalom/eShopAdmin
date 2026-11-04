using System.Net.Http.Json;
using System.Text.Json;

namespace eShopAdmin.Catalog;

/// <summary>
/// Reads names from Catalog.API (anonymous, v1.0) so admins pick types and brands by name. Catalog is optional for the
/// admin app: every lookup returns null or an empty result on failure, and logs a warning with no user data.
/// </summary>
public sealed class CatalogLookupClient(HttpClient http, ILogger<CatalogLookupClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CatalogOption>?> GetTypesAsync(CancellationToken ct) =>
        await TryGetAsync<List<CatalogType>>("api/catalog/catalogtypes", "types", ct) is { } types
            ? types.Where(t => t?.Type is not null).Select(t => new CatalogOption(t.Id, t.Type)).ToList()
            : null;

    public async Task<IReadOnlyList<CatalogOption>?> GetBrandsAsync(CancellationToken ct) =>
        await TryGetAsync<List<CatalogBrand>>("api/catalog/catalogbrands", "brands", ct) is { } brands
            ? brands.Where(b => b?.Brand is not null).Select(b => new CatalogOption(b.Id, b.Brand)).ToList()
            : null;

    public async Task<IReadOnlyDictionary<int, string>> GetItemNamesAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var query = string.Join('&', ids.Distinct().Select(id => $"ids={id}"));
        var items = await TryGetAsync<List<CatalogItem>>($"api/catalog/items/by?{query}", "items", ct);
        return items is null
            ? new Dictionary<int, string>()
            : items.Where(i => i?.Name is not null).GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First().Name);
    }

    // A caller's own cancellation still propagates. Everything else (down, slow, 5xx, bad JSON) is "unavailable".
    private async Task<T?> TryGetAsync<T>(string url, string lookup, CancellationToken ct)
        where T : class
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Catalog {Lookup} lookup unavailable: {Reason}", lookup, ex.GetType().Name);
            return null;
        }
    }

    private sealed record CatalogType(int Id, string Type);

    private sealed record CatalogBrand(int Id, string Brand);

    private sealed record CatalogItem(int Id, string Name);
}
