using System.Text.Json;

namespace eShopAdmin.BusinessRules;

// Copies of the BusinessRules.API v1 wire shapes (camelCase JSON, enums as strings).
public enum RuleTargeting
{
    Global,
    Targeted,
}

public enum RuleVersionStatus
{
    Draft,
    Published,
    Superseded,
}

public enum AuditAction
{
    DraftCreated,
    DraftUpdated,
    Published,
    GuardOverridden,
}

public record RuleTypeDto(
    string Id,
    string Name,
    bool AffectsPrice,
    RuleTargeting Targeting,
    JsonElement Default,
    JsonElement ParameterSchema);

public record RuleVersionDto(
    Guid Id,
    string RuleTypeId,
    string Scope,
    int VersionNumber,
    JsonElement Parameters,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    RuleVersionStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? PublishedBy,
    DateTimeOffset? PublishedAt);

public record AuditEntryDto(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorId,
    AuditAction Action,
    string RuleTypeId,
    string Scope,
    Guid? RuleVersionId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason);

public record CreateDraftRequest(
    string RuleTypeId,
    string Scope,
    JsonElement Parameters,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public record UpdateDraftRequest(
    JsonElement Parameters,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public record PaginatedItems<T>(int PageIndex, int PageSize, long Count, IReadOnlyList<T> Data);
