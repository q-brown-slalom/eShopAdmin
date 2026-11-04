using System.Globalization;
using System.Text.Json;
using eShopAdmin.BusinessRules;

namespace eShopAdmin.Rules;

// Display logic of the audit log page, kept out of the markup so MSTest can cover it.
public static class AuditLogView
{
    public const int PageSize = 20;

    // Keeps pageIndex * pageSize far from int overflow on the API side. A page this far out is simply empty.
    public const int MaxPage = 1_000_000;

    /// <summary>The 0-based page from the query string. Anything that isn't a non-negative number is the first page.</summary>
    public static int ParsePage(string? raw)
    {
        if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var page))
        {
            return 0;
        }

        return (int)Math.Min(page, MaxPage);
    }

    public static bool HasNext(PaginatedItems<AuditEntryDto> page) =>
        ((long)page.PageIndex + 1) * page.PageSize < page.Count;

    public static int PageCount(long count, int pageSize) =>
        pageSize <= 0 ? 0 : (int)((count + pageSize - 1) / pageSize);

    public static string By(string actorId, string? signedInSubject) =>
        !string.IsNullOrEmpty(signedInSubject) && string.Equals(actorId, signedInSubject, StringComparison.Ordinal)
            ? $"{actorId} (you)"
            : actorId;

    /// <summary>Indents the recorded JSON. Text that isn't valid JSON is returned as recorded, never an exception.</summary>
    public static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "(none)";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
