using System.Globalization;
using System.Text.Json;

namespace eShopAdmin.Rules;

public static class ParameterSummary
{
    private const string Custom = "Custom value";

    public static string Describe(string ruleTypeId, JsonElement parameters)
    {
        var input = DraftInput.FromParameters(ruleTypeId, parameters, default, null);
        if (input is null)
        {
            return Custom;
        }

        return ruleTypeId switch
        {
            RuleForms.GracePeriod => input.GracePeriod.Minutes == 1 ? "1 minute" : $"{input.GracePeriod.Minutes} minutes",
            RuleForms.ShortfallPolicy => input.ShortfallPolicy.Policy == ShortfallPolicyForm.AllOrNothing ? "All or nothing" : "Partial fulfilment",
            RuleForms.ShippingFee => DescribeShippingFee(input.ShippingFee),
            RuleForms.PriceAdjustment => input.PriceAdjustment.Entries.Count switch
            {
                0 => "No price adjustments",
                1 => "1 price adjustment",
                var count => $"{count} price adjustments",
            },
            RuleForms.Promotion => $"{input.Promotion.LinePromotions.Count} line, {input.Promotion.OrderPromotions.Count} order promotions",
            _ => Custom,
        };
    }

    private static string DescribeShippingFee(ShippingFeeForm form)
    {
        var fee = $"Fee {Money(form.Fee)}";
        return form.FreeShippingThreshold is { } threshold ? $"{fee}, free from {Money(threshold)}" : fee;
    }

    private static string Money(decimal amount) => "$" + amount.ToString("N2", CultureInfo.InvariantCulture);
}
