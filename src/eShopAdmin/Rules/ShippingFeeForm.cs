using System.Text.Json;

namespace eShopAdmin.Rules;

public class ShippingFeeForm
{
    public decimal Fee { get; set; }

    public decimal? FreeShippingThreshold { get; set; }

    internal static ShippingFeeForm? FromJson(JsonElement json)
    {
        // The API schema doesn't require "fee"; a missing one means 0.
        if (!FormJson.IsObjectWith(json, "fee", "freeShippingThreshold"))
        {
            return null;
        }

        var fee = 0m;
        if (json.TryGetProperty("fee", out _) && !FormJson.TryReadDecimal(json, "fee", out fee))
        {
            return null;
        }

        return FormJson.TryReadNullableDecimal(json, "freeShippingThreshold", out var threshold)
            ? new ShippingFeeForm { Fee = fee, FreeShippingThreshold = threshold }
            : null;
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("fee", Fee);
        if (FreeShippingThreshold is { } threshold)
        {
            writer.WriteNumber("freeShippingThreshold", threshold);
        }
        else
        {
            writer.WriteNull("freeShippingThreshold");
        }

        writer.WriteEndObject();
    }
}
