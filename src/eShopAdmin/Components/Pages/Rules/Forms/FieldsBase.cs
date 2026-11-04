using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace eShopAdmin.Components.Pages.Rules.Forms;

/// <summary>
/// Shared by the field groups of the draft form. Each input gets an explicit <c>name</c>, because a child component's own
/// parameter name would otherwise leak into the one Blazor derives, and the posted value would not bind back to the
/// page's model.
/// </summary>
public abstract class FieldsBase : ComponentBase
{
    [CascadingParameter]
    protected EditContext EditContext { get; set; } = default!;

    /// <summary>The form field path of the model this group edits, for example <c>Input.GracePeriod</c>.</summary>
    [Parameter, EditorRequired]
    public string NamePrefix { get; set; } = string.Empty;

    /// <summary>The posted field name of <paramref name="member"/>, a member or <c>List[0].Member</c> path under the prefix.</summary>
    protected string NameOf(string member) => $"{NamePrefix}.{member}";

    protected string IdOf(string member) => ToId(NameOf(member));

    protected string ErrorIdOf(string member) => $"{IdOf(member)}-error";

    protected string HintIdOf(string member) => $"{IdOf(member)}-hint";

    /// <summary>
    /// The value of <c>aria-describedby</c>: the hint (if any) and, only while the field has errors, its error message.
    /// Both ids are those of the elements the caller renders.
    /// </summary>
    protected string? Describe(object model, string member, string errorId, string? hintId = null)
    {
        var ids = new List<string>(2);
        if (hintId is not null)
        {
            ids.Add(hintId);
        }

        if (EditContext.GetValidationMessages(new FieldIdentifier(model, member)).Any())
        {
            ids.Add(errorId);
        }

        return ids.Count == 0 ? null : string.Join(' ', ids);
    }

    // Dots and brackets of a field path make awkward ids, so every other character becomes a single dash.
    private static string ToId(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
