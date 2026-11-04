using System.Text.RegularExpressions;

namespace eShopAdmin.Rules;

public sealed record FieldErrors(IReadOnlyDictionary<string, string[]> ByField, IReadOnlyList<string> Summary);

public static partial class ValidationErrorMapper
{
    public static FieldErrors Map(string ruleTypeId, IReadOnlyDictionary<string, string[]> errors)
    {
        var byField = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var summary = new List<string>();
        var formProperty = FormProperty(ruleTypeId);

        foreach (var (key, messages) in errors)
        {
            foreach (var message in messages)
            {
                var field = FieldFor(key, message, formProperty, out var text);
                if (field is null)
                {
                    summary.Add(message);
                    continue;
                }

                if (!byField.TryGetValue(field, out var list))
                {
                    byField[field] = list = [];
                }

                list.Add(text);
            }
        }

        return new FieldErrors(byField.ToDictionary(e => e.Key, e => e.Value.ToArray()), summary);
    }

    private static string? FieldFor(string key, string message, string? formProperty, out string text)
    {
        text = message;
        if (key.Equals("EffectiveFrom", StringComparison.OrdinalIgnoreCase))
        {
            return "EffectiveFromUtc";
        }

        if (key.Equals("EffectiveTo", StringComparison.OrdinalIgnoreCase))
        {
            return "EffectiveToUtc";
        }

        if (formProperty is null || !key.Equals("Parameters", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = JsonPathPrefix().Match(message);
        if (!match.Success)
        {
            return null;
        }

        text = message[match.Length..];
        return $"{formProperty}.{PascalCase(match.Groups["path"].Value)}";
    }

    private static string? FormProperty(string ruleTypeId) => ruleTypeId switch
    {
        RuleForms.GracePeriod => nameof(DraftInput.GracePeriod),
        RuleForms.ShortfallPolicy => nameof(DraftInput.ShortfallPolicy),
        RuleForms.ShippingFee => nameof(DraftInput.ShippingFee),
        RuleForms.PriceAdjustment => nameof(DraftInput.PriceAdjustment),
        RuleForms.Promotion => nameof(DraftInput.Promotion),
        _ => null,
    };

    private static string PascalCase(string path) =>
        PathSegmentStart().Replace(path, m => m.Value.ToUpperInvariant());

    [GeneratedRegex(@"^(?<path>[a-z]\w*(\[\d+\])?(\.[a-z]\w*(\[\d+\])?)*): ", RegexOptions.CultureInvariant)]
    private static partial Regex JsonPathPrefix();

    [GeneratedRegex(@"(?<=^|\.)[a-z]", RegexOptions.CultureInvariant)]
    private static partial Regex PathSegmentStart();
}
