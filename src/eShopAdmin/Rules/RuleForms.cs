using System.Text.Json;

namespace eShopAdmin.Rules;

public static class RuleForms
{
    public const string GracePeriod = "ORD-001";
    public const string ShortfallPolicy = "ORD-013";
    public const string ShippingFee = "SHP-001";
    public const string PriceAdjustment = "PRC-001";
    public const string Promotion = "PRM-001";

    public static bool IsSupported(string ruleTypeId) =>
        ruleTypeId is GracePeriod or ShortfallPolicy or ShippingFee or PriceAdjustment or Promotion;
}

// Strict readers: each returns false for anything the typed forms can't represent without loss.
internal static class FormJson
{
    public static bool IsObjectWith(JsonElement element, params string[] members)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!members.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryReadInt(JsonElement obj, string name, out int value)
    {
        value = 0;
        return obj.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    public static bool TryReadNullableInt(JsonElement obj, string name, out int? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var number))
        {
            return false;
        }

        value = number;
        return true;
    }

    public static bool TryReadDecimal(JsonElement obj, string name, out decimal value)
    {
        value = 0;
        return obj.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDecimal(out value);
    }

    public static bool TryReadNullableDecimal(JsonElement obj, string name, out decimal? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var number))
        {
            return false;
        }

        value = number;
        return true;
    }

    public static bool TryReadString(JsonElement obj, string name, out string value)
    {
        value = string.Empty;
        if (!obj.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()!;
        return true;
    }

    public static bool TryReadEnum(JsonElement obj, string name, string[] allowed, out string value) =>
        TryReadString(obj, name, out value) && allowed.Contains(value, StringComparer.Ordinal);

    public static bool TryReadArray(JsonElement obj, string name, out JsonElement array)
    {
        array = default;
        return obj.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array;
    }

    public static JsonElement Build(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}
