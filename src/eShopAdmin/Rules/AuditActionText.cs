using eShopAdmin.BusinessRules;

namespace eShopAdmin.Rules;

public static class AuditActionText
{
    public static string Describe(AuditAction action) => action switch
    {
        AuditAction.DraftCreated => "Draft created",
        AuditAction.DraftUpdated => "Draft updated",
        AuditAction.Published => "Published",
        AuditAction.GuardOverridden => "Guard overridden",
        _ => action.ToString(),
    };
}
