using System.Globalization;
using eShopAdmin.BusinessRules;

namespace eShopAdmin.Rules;

public sealed record RuleStatusSummary(RuleVersionDto? Current, RuleVersionDto? NextScheduled, int DraftCount)
{
    public static RuleStatusSummary From(IReadOnlyList<RuleVersionDto> versions, DateTimeOffset now)
    {
        var published = versions.Where(v => v.Status == RuleVersionStatus.Published).ToList();

        var current = published
            .Where(v => v.EffectiveFrom <= now && (v.EffectiveTo is null || now < v.EffectiveTo))
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        var next = published
            .Where(v => v.EffectiveFrom > now)
            .OrderBy(v => v.EffectiveFrom)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return new RuleStatusSummary(current, next, versions.Count(v => v.Status == RuleVersionStatus.Draft));
    }

    public static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
