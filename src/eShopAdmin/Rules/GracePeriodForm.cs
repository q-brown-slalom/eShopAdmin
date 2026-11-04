using System.Text.Json;

namespace eShopAdmin.Rules;

public class GracePeriodForm
{
    public int Minutes { get; set; } = 1;

    internal static GracePeriodForm? FromJson(JsonElement json) =>
        FormJson.IsObjectWith(json, "minutes") && FormJson.TryReadInt(json, "minutes", out var minutes)
            ? new GracePeriodForm { Minutes = minutes }
            : null;

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("minutes", Minutes);
        writer.WriteEndObject();
    }
}
