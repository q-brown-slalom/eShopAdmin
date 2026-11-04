using System.Text.Json;

namespace eShopAdmin.Rules;

public class PriceAdjustmentForm
{
    public List<PriceAdjustmentEntryForm> Entries { get; set; } = [];

    internal static PriceAdjustmentForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "entries") || !FormJson.TryReadArray(json, "entries", out var array))
        {
            return null;
        }

        var form = new PriceAdjustmentForm();
        foreach (var item in array.EnumerateArray())
        {
            var entry = PriceAdjustmentEntryForm.FromJson(item);
            if (entry is null)
            {
                return null;
            }

            form.Entries.Add(entry);
        }

        return form;
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("entries");
        foreach (var entry in Entries)
        {
            entry.Write(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

public class PriceAdjustmentEntryForm
{
    public const string PercentOff = "PercentOff";
    public const string FixedPrice = "FixedPrice";

    public static readonly string[] Modes = [PercentOff, FixedPrice];

    public TargetForm Target { get; set; } = new();

    public string Mode { get; set; } = PercentOff;

    public decimal Value { get; set; }

    internal static PriceAdjustmentEntryForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "target", "mode", "value")
            || !json.TryGetProperty("target", out var targetJson)
            || TargetForm.FromJson(targetJson) is not { } target
            || !FormJson.TryReadEnum(json, "mode", Modes, out var mode)
            || !FormJson.TryReadDecimal(json, "value", out var value))
        {
            return null;
        }

        return new PriceAdjustmentEntryForm { Target = target, Mode = mode, Value = value };
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("target");
        Target.Write(writer);
        writer.WriteString("mode", Mode);
        writer.WriteNumber("value", Value);
        writer.WriteEndObject();
    }
}
