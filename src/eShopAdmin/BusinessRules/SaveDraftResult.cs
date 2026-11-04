namespace eShopAdmin.BusinessRules;

public abstract record SaveDraftResult
{
    private SaveDraftResult()
    {
    }

    public sealed record Saved(RuleVersionDto Version) : SaveDraftResult;

    /// <summary>Validation failed. Keys are JSON paths such as <c>parameters.entries[2].value</c>.</summary>
    public sealed record Invalid(IReadOnlyDictionary<string, string[]> Errors) : SaveDraftResult;

    public sealed record Conflict(string Detail) : SaveDraftResult;

    public sealed record NotFound : SaveDraftResult;
}
