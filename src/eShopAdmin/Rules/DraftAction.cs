using System.Globalization;

namespace eShopAdmin.Rules;

public enum DraftActionKind
{
    Save,
    Add,
    Remove,
}

/// <summary>
/// What a submit button of the draft form asks for. The button's value is <c>save</c>, <c>add-{list}</c> or
/// <c>remove-{list}-{index}</c>, with the list name in lower case.
/// </summary>
public sealed record DraftAction(DraftActionKind Kind, string? List, int Index)
{
    private const string AddPrefix = "add-";
    private const string RemovePrefix = "remove-";

    /// <summary>
    /// The name of the submit buttons. It mustn't be <c>action</c> (or any other property of a form element), because a
    /// named control shadows that property and Blazor's enhanced forms read <c>form.action</c> to know where to post.
    /// </summary>
    public const string FieldName = "intent";

    private static readonly string[] Lists = [DraftInput.Entries, DraftInput.LinePromotions, DraftInput.OrderPromotions];

    public static string AddValue(string list) => AddPrefix + list.ToLowerInvariant();

    public static string RemoveValue(string list, int index) =>
        $"{RemovePrefix}{list.ToLowerInvariant()}-{index.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>A missing value is an implicit submit (Enter in a field), which saves. An unknown value returns null.</summary>
    public static DraftAction? Parse(string? value)
    {
        if (value is null || value == "save")
        {
            return new DraftAction(DraftActionKind.Save, null, -1);
        }

        if (value.StartsWith(AddPrefix, StringComparison.Ordinal) && FindList(value[AddPrefix.Length..]) is { } addList)
        {
            return new DraftAction(DraftActionKind.Add, addList, -1);
        }

        if (value.StartsWith(RemovePrefix, StringComparison.Ordinal))
        {
            var rest = value[RemovePrefix.Length..];
            var dash = rest.LastIndexOf('-');
            if (dash > 0
                && FindList(rest[..dash]) is { } removeList
                && int.TryParse(rest[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                return new DraftAction(DraftActionKind.Remove, removeList, index);
            }
        }

        return null;
    }

    private static string? FindList(string name) =>
        Lists.FirstOrDefault(list => string.Equals(list.ToLowerInvariant(), name, StringComparison.Ordinal));
}
