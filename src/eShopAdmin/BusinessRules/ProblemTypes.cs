namespace eShopAdmin.BusinessRules;

// ProblemDetails `type` URIs from BusinessRules.API (PublishProblemTypes).
public static class ProblemTypes
{
    public const string ConcurrentPublish = "https://eshop/problems/concurrent-publish";
    public const string V1ClientsActive = "https://eshop/problems/v1-clients-active";
    public const string PublishGuardUnavailable = "https://eshop/problems/publish-guard-unavailable";
    public const string InvalidGuardOverrideReason = "https://eshop/problems/invalid-guard-override-reason";
}
