using System.Text.Json;

namespace eShopAdmin.Rules;

public class PromotionForm
{
    public List<LinePromotionForm> LinePromotions { get => field ??= []; set => field = value ?? []; }

    public List<OrderPromotionForm> OrderPromotions { get => field ??= []; set => field = value ?? []; }

    internal static PromotionForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "linePromotions", "orderPromotions")
            || !FormJson.TryReadArray(json, "linePromotions", out var lines)
            || !FormJson.TryReadArray(json, "orderPromotions", out var orders))
        {
            return null;
        }

        var form = new PromotionForm();
        foreach (var item in lines.EnumerateArray())
        {
            if (LinePromotionForm.FromJson(item) is not { } line)
            {
                return null;
            }

            form.LinePromotions.Add(line);
        }

        foreach (var item in orders.EnumerateArray())
        {
            if (OrderPromotionForm.FromJson(item) is not { } order)
            {
                return null;
            }

            form.OrderPromotions.Add(order);
        }

        return form;
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("linePromotions");
        foreach (var line in LinePromotions)
        {
            line.Write(writer);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("orderPromotions");
        foreach (var order in OrderPromotions)
        {
            order.Write(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

public class LinePromotionForm
{
    public const string PercentOff = "PercentOff";
    public const string AmountOff = "AmountOff";

    public static readonly string[] Kinds = [PercentOff, AmountOff];

    public TargetForm Target { get => field ??= new(); set => field = value ?? new(); }

    public string Kind { get; set; } = PercentOff;

    public decimal Value { get; set; }

    public int? MinUnits { get; set; }

    public string Label { get; set; } = string.Empty;

    internal static LinePromotionForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "target", "kind", "value", "minUnits", "label")
            || !json.TryGetProperty("target", out var targetJson)
            || TargetForm.FromJson(targetJson) is not { } target
            || !FormJson.TryReadEnum(json, "kind", Kinds, out var kind)
            || !FormJson.TryReadDecimal(json, "value", out var value)
            || !FormJson.TryReadNullableInt(json, "minUnits", out var minUnits)
            || !FormJson.TryReadString(json, "label", out var label))
        {
            return null;
        }

        return new LinePromotionForm { Target = target, Kind = kind, Value = value, MinUnits = minUnits, Label = label };
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("target");
        Target.Write(writer);
        writer.WriteString("kind", Kind ?? string.Empty);
        writer.WriteNumber("value", Value);
        if (MinUnits is { } minUnits)
        {
            writer.WriteNumber("minUnits", minUnits);
        }
        else
        {
            writer.WriteNull("minUnits");
        }

        writer.WriteString("label", Label ?? string.Empty);
        writer.WriteEndObject();
    }
}

public class OrderPromotionForm
{
    public string Kind { get; set; } = LinePromotionForm.PercentOff;

    public decimal Value { get; set; }

    public decimal MinSubtotal { get; set; }

    public string Label { get; set; } = string.Empty;

    internal static OrderPromotionForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "kind", "value", "minSubtotal", "label")
            || !FormJson.TryReadEnum(json, "kind", LinePromotionForm.Kinds, out var kind)
            || !FormJson.TryReadDecimal(json, "value", out var value)
            || !FormJson.TryReadDecimal(json, "minSubtotal", out var minSubtotal)
            || !FormJson.TryReadString(json, "label", out var label))
        {
            return null;
        }

        return new OrderPromotionForm { Kind = kind, Value = value, MinSubtotal = minSubtotal, Label = label };
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Kind ?? string.Empty);
        writer.WriteNumber("value", Value);
        writer.WriteNumber("minSubtotal", MinSubtotal);
        writer.WriteString("label", Label ?? string.Empty);
        writer.WriteEndObject();
    }
}
