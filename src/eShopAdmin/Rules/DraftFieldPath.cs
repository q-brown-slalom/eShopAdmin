using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;

namespace eShopAdmin.Rules;

/// <summary>
/// Finds the model and member behind a field key such as <c>PriceAdjustment.Entries[2].Value</c>, as produced by
/// <see cref="ValidationErrorMapper"/>, so a server message can be attached to the input that edits it.
/// </summary>
public static partial class DraftFieldPath
{
    public static FieldIdentifier? Resolve(DraftInput input, string path)
    {
        object current = input;
        var segments = path.Split('.');

        for (var i = 0; i < segments.Length; i++)
        {
            var match = Segment().Match(segments[i]);
            if (!match.Success)
            {
                return null;
            }

            var property = current.GetType().GetProperty(match.Groups["name"].Value, BindingFlags.Public | BindingFlags.Instance);
            if (property is null || property.GetIndexParameters().Length > 0)
            {
                return null;
            }

            var last = i == segments.Length - 1;
            if (!match.Groups["index"].Success)
            {
                if (last)
                {
                    return new FieldIdentifier(current, property.Name);
                }

                if (property.GetValue(current) is not { } next)
                {
                    return null;
                }

                current = next;
                continue;
            }

            // An indexed segment names a list item, which has no input of its own, so it can't be the last one.
            if (last
                || property.GetValue(current) is not IList list
                || !int.TryParse(match.Groups["index"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || index >= list.Count
                || list[index] is not { } item)
            {
                return null;
            }

            current = item;
        }

        return null;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z]\w*)(\[(?<index>\d+)\])?$", RegexOptions.CultureInvariant)]
    private static partial Regex Segment();
}
