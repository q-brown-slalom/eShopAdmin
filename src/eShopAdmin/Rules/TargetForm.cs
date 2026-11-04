using System.Text.Json;

namespace eShopAdmin.Rules;

public class TargetForm
{
    public const string All = "All";
    public const string Brand = "Brand";
    public const string Type = "Type";
    public const string Item = "Item";

    public static readonly string[] Kinds = [All, Brand, Type, Item];

    public string Kind { get; set; } = All;

    public int? Id { get; set; }

    internal static TargetForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "kind", "id")
            || !FormJson.TryReadEnum(json, "kind", Kinds, out var kind)
            || !FormJson.TryReadNullableInt(json, "id", out var id)
            || (kind == All && id is not null))
        {
            return null;
        }

        return new TargetForm { Kind = kind, Id = id };
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Kind);
        if (Kind != All && Id is { } id)
        {
            writer.WriteNumber("id", id);
        }
        else
        {
            writer.WriteNull("id");
        }

        writer.WriteEndObject();
    }
}
