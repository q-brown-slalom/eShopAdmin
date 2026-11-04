using System.Text.Json;

namespace eShopAdmin.Rules;

public sealed class DraftInput
{
    public const string Entries = "Entries";
    public const string LinePromotions = "LinePromotions";
    public const string OrderPromotions = "OrderPromotions";

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    public GracePeriodForm GracePeriod { get; set; } = new();

    public ShortfallPolicyForm ShortfallPolicy { get; set; } = new();

    public ShippingFeeForm ShippingFee { get; set; } = new();

    public PriceAdjustmentForm PriceAdjustment { get; set; } = new();

    public PromotionForm Promotion { get; set; } = new();

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
}
