using System.Globalization;

namespace eShopAdmin.Rules;

/// <summary>The copy and the small input rule of the publish page.</summary>
public static class PublishCopy
{
    public static readonly string PastStart = "A start time in the past takes effect as soon as the version is published.";
    public static readonly string PriceWarning = "This rule changes prices. Shoppers see the new prices within 60 seconds of it taking effect.";
    public static readonly string Guard = "Older app versions placed orders in the last 7 days and may show out-of-date prices.";
    public static readonly string ReasonLabel = "Reason for publishing anyway (required, up to 500 characters)";
    public static readonly string GuardUnavailable = "Publishing is paused because app usage couldn't be checked. Try again in a few minutes.";
    public static readonly string ConcurrentPublish = "Another publish happened at the same time. Reload this page and try again.";

    public static string Published(long snapshotVersion) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Published. Rules snapshot {snapshotVersion}. Shoppers see the change within 60 seconds of it taking effect.");

    public static string Conflict(string detail) => "This version can't be changed any more: " + detail;

    /// <summary>The reason to send: trimmed, and null when there is nothing left, so an empty reason is never sent.</summary>
    public static string? NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
