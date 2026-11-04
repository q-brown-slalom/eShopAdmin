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

    // Static SSR can't swap the id control when Kind changes, so the page shows one picker per kind side by side
    // (type and brand by name, item by id). ResolveId copies the one that matches Kind into Id.
    public int? TypeId { get; set; }

    public int? BrandId { get; set; }

    public int? ItemId { get; set; }

    internal void ResolveId()
    {
        Id = Kind switch
        {
            Type => TypeId ?? Id,
            Brand => BrandId ?? Id,
            Item => ItemId ?? Id,
            _ => null,
        };
    }

    internal static TargetForm? FromJson(JsonElement json)
    {
        if (!FormJson.IsObjectWith(json, "kind", "id")
            || !FormJson.TryReadEnum(json, "kind", Kinds, out var kind)
            || !FormJson.TryReadNullableInt(json, "id", out var id)
            || (kind == All && id is not null))
        {
            return null;
        }

        return new TargetForm
        {
            Kind = kind,
            Id = id,
            TypeId = kind == Type ? id : null,
            BrandId = kind == Brand ? id : null,
            ItemId = kind == Item ? id : null,
        };
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Kind ?? string.Empty);
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
