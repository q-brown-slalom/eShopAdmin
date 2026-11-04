using System.Globalization;

namespace eShopAdmin.Rules;

/// <summary>
/// The success banner on the history page. A redirect carries a fixed code and a number, never text, so a crafted
/// link can't show a message the server didn't write.
/// </summary>
public static class StatusBanner
{
    public const string DraftSaved = "draft-saved";
    public const string Published = "published";

    public static string DraftSavedQuery(int versionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"status={DraftSaved}&v={versionNumber}");

    public static string PublishedQuery(long snapshotVersion) =>
        string.Create(CultureInfo.InvariantCulture, $"status={Published}&snapshot={snapshotVersion}");

    /// <summary>The copy for a code and its number, or null for an unknown code or a missing or malformed number.</summary>
    public static string? Text(string? code, string? version, string? snapshot) => code switch
    {
        DraftSaved when ParseNumber(version) is { } v => string.Create(CultureInfo.InvariantCulture, $"Draft v{v} saved."),
        Published when ParseNumber(snapshot) is { } s => PublishCopy.Published(s),
        _ => null,
    };

    private static long? ParseNumber(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}
