using System.Text.Json;

namespace eShopAdmin.Rules;

public class ShortfallPolicyForm
{
    public const string AllOrNothing = "AllOrNothing";
    public const string PartialFulfilment = "PartialFulfilment";

    public static readonly string[] Policies = [AllOrNothing, PartialFulfilment];

    public string Policy { get; set; } = AllOrNothing;

    internal static ShortfallPolicyForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "policy"))
        {
            return null;
        }

        // The API schema doesn't require "policy"; a missing one means the default.
        if (!json.TryGetProperty("policy", out _))
        {
            return new ShortfallPolicyForm();
        }

        return FormJson.TryReadEnum(json, "policy", Policies, out var policy) ? new ShortfallPolicyForm { Policy = policy } : null;
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("policy", Policy);
        writer.WriteEndObject();
    }
}
