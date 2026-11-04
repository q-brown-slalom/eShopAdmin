namespace eShopAdmin.BusinessRules;

public abstract record PublishResult
{
    private PublishResult()
    {
    }

    public sealed record Published(long SnapshotVersion) : PublishResult;

    public sealed record V1ClientsActive(string Detail) : PublishResult;

    public sealed record GuardUnavailable : PublishResult;

    public sealed record InvalidOverrideReason(string Detail) : PublishResult;

    public sealed record ConcurrentPublish : PublishResult;

    /// <summary>The version can't be published any more (not a draft, window ended, rule type gone).</summary>
    public sealed record Conflict(string Detail) : PublishResult;

    public sealed record NotFound : PublishResult;

    public sealed record Forbidden : PublishResult;
}
