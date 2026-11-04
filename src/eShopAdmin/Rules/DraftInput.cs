using System.Text.Json;
using eShopAdmin.BusinessRules;

namespace eShopAdmin.Rules;

public sealed class DraftInput
{
    public const string Entries = "Entries";
    public const string LinePromotions = "LinePromotions";
    public const string OrderPromotions = "OrderPromotions";

    private const string DefaultScope = "default";

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    // The SSR form binder assigns null to a nested form when the post has no field for it, so each one is null-safe.
    public GracePeriodForm GracePeriod { get => field ??= new(); set => field = value ?? new(); }

    public ShortfallPolicyForm ShortfallPolicy { get => field ??= new(); set => field = value ?? new(); }

    public ShippingFeeForm ShippingFee { get => field ??= new(); set => field = value ?? new(); }

    public PriceAdjustmentForm PriceAdjustment { get => field ??= new(); set => field = value ?? new(); }

    public PromotionForm Promotion { get => field ??= new(); set => field = value ?? new(); }

    /// <summary>
    /// Strict: returns null for an unsupported rule type, or when the JSON has a member the form doesn't model,
    /// lacks a required one, or has the wrong kind, so saving the form can't drop data.
    /// </summary>
    public static DraftInput? FromParameters(string ruleTypeId, JsonElement parameters, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        var input = new DraftInput
        {
            EffectiveFromUtc = effectiveFrom.UtcDateTime,
            EffectiveToUtc = effectiveTo?.UtcDateTime,
        };

        switch (ruleTypeId)
        {
            case RuleForms.GracePeriod when GracePeriodForm.FromJson(parameters) is { } grace:
                input.GracePeriod = grace;
                return input;
            case RuleForms.ShortfallPolicy when ShortfallPolicyForm.FromJson(parameters) is { } shortfall:
                input.ShortfallPolicy = shortfall;
                return input;
            case RuleForms.ShippingFee when ShippingFeeForm.FromJson(parameters) is { } shipping:
                input.ShippingFee = shipping;
                return input;
            case RuleForms.PriceAdjustment when PriceAdjustmentForm.FromJson(parameters) is { } adjustment:
                input.PriceAdjustment = adjustment;
                return input;
            case RuleForms.Promotion when PromotionForm.FromJson(parameters) is { } promotion:
                input.Promotion = promotion;
                return input;
            default:
                return null;
        }
    }

    /// <summary>The API parameter JSON: camelCase, string enums, nulls written as <c>null</c>.</summary>
    public JsonElement ToParameters(string ruleTypeId) => ruleTypeId switch
    {
        RuleForms.GracePeriod => FormJson.Build(GracePeriod.Write),
        RuleForms.ShortfallPolicy => FormJson.Build(ShortfallPolicy.Write),
        RuleForms.ShippingFee => FormJson.Build(ShippingFee.Write),
        RuleForms.PriceAdjustment => FormJson.Build(PriceAdjustment.Write),
        RuleForms.Promotion => FormJson.Build(Promotion.Write),
        _ => throw new ArgumentException($"Rule type '{ruleTypeId}' has no form.", nameof(ruleTypeId)),
    };

    /// <summary>
    /// Checks the effective window before any API call. Both times are read as UTC. An end that isn't after the start is
    /// an error on <c>EffectiveToUtc</c>; a start before <paramref name="now"/> isn't an error, it is reported through
    /// <paramref name="startsInPast"/> so the page can show a notice.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ValidateWindow(DateTimeOffset now, out bool startsInPast)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        startsInPast = false;

        if (EffectiveFromUtc == default)
        {
            errors["EffectiveFromUtc"] = ["Enter a start date and time (UTC)."];
            return errors;
        }

        var from = AsUtc(EffectiveFromUtc);
        startsInPast = from < now;

        if (EffectiveToUtc is { } to && AsUtc(to) <= from)
        {
            errors["EffectiveToUtc"] = ["The end must be later than the start."];
        }

        return errors;
    }

    public CreateDraftRequest ToCreateRequest(string ruleTypeId) =>
        new(ruleTypeId, DefaultScope, ToParameters(ruleTypeId), AsUtc(EffectiveFromUtc), EffectiveToUtc is { } to ? AsUtc(to) : null);

    public UpdateDraftRequest ToUpdateRequest(string ruleTypeId) =>
        new(ToParameters(ruleTypeId), AsUtc(EffectiveFromUtc), EffectiveToUtc is { } to ? AsUtc(to) : null);

    /// <summary>The next whole minute at or after <paramref name="now"/>, as the UTC default start of a new draft.</summary>
    public static DateTime RoundUpToMinute(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        var floor = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
        return floor == utc ? floor : floor.AddMinutes(1);
    }

    /// <summary>Copies each target's picker for its kind into the id the API uses. Call after binding a posted form.</summary>
    public void ResolveTargets()
    {
        foreach (var entry in PriceAdjustment.Entries)
        {
            entry.Target.ResolveId();
        }

        foreach (var line in Promotion.LinePromotions)
        {
            line.Target.ResolveId();
        }
    }

    public void AddEntry(string list)
    {
        switch (list)
        {
            case Entries:
                PriceAdjustment.Entries.Add(new PriceAdjustmentEntryForm());
                break;
            case LinePromotions:
                Promotion.LinePromotions.Add(new LinePromotionForm());
                break;
            case OrderPromotions:
                Promotion.OrderPromotions.Add(new OrderPromotionForm());
                break;
            default:
                throw UnknownList(list);
        }
    }

    /// <summary>Removes the entry at <paramref name="index"/>; an index outside the list is ignored.</summary>
    public void RemoveEntry(string list, int index)
    {
        switch (list)
        {
            case Entries:
                RemoveAt(PriceAdjustment.Entries, index);
                break;
            case LinePromotions:
                RemoveAt(Promotion.LinePromotions, index);
                break;
            case OrderPromotions:
                RemoveAt(Promotion.OrderPromotions, index);
                break;
            default:
                throw UnknownList(list);
        }
    }

    private static void RemoveAt<T>(List<T> items, int index)
    {
        if (index >= 0 && index < items.Count)
        {
            items.RemoveAt(index);
        }
    }

    private static ArgumentException UnknownList(string list) => new($"Unknown entry list '{list}'.", nameof(list));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
