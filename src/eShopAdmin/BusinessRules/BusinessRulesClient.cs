using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace eShopAdmin.BusinessRules;

public sealed class BusinessRulesClient(HttpClient http, ILogger<BusinessRulesClient> logger)
{
    private const string DefaultScope = "default";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<RuleTypeDto>> GetRuleTypesAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/rules/types", ct);
        return await ReadAsync<List<RuleTypeDto>>(response, ct);
    }

    public async Task<IReadOnlyList<RuleVersionDto>> GetVersionsAsync(string ruleTypeId, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"api/rules/versions?ruleTypeId={Uri.EscapeDataString(ruleTypeId)}&scope={DefaultScope}", ct);
        return await ReadAsync<List<RuleVersionDto>>(response, ct);
    }

    public async Task<SaveDraftResult> CreateDraftAsync(CreateDraftRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("api/rules/versions", request, JsonOptions, ct);
        var result = await MapSaveAsync(response, ct);
        if (result is SaveDraftResult.Saved)
        {
            logger.LogInformation("Rule draft {RuleTypeId} saved", request.RuleTypeId);
        }

        return result;
    }

    public async Task<SaveDraftResult> UpdateDraftAsync(Guid id, UpdateDraftRequest request, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync($"api/rules/versions/{id}", request, JsonOptions, ct);
        var result = await MapSaveAsync(response, ct);
        if (result is SaveDraftResult.Saved saved)
        {
            logger.LogInformation("Rule draft {RuleTypeId} saved", saved.Version.RuleTypeId);
        }

        return result;
    }

    public async Task<PublishResult> PublishAsync(Guid id, string? guardOverrideReason, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/rules/versions/{id}/publish", new PublishRequest(guardOverrideReason), JsonOptions, ct);
        var result = await MapPublishAsync(response, ct);
        if (result is PublishResult.Published published)
        {
            logger.LogInformation(
                "Rule version {RuleVersionId} published as snapshot {SnapshotVersion}", id, published.SnapshotVersion);
        }

        return result;
    }

    public async Task<PaginatedItems<AuditEntryDto>> GetAuditAsync(int pageIndex, int pageSize, CancellationToken ct)
    {
        using var response = await http.GetAsync($"api/rules/audit?pageIndex={pageIndex}&pageSize={pageSize}", ct);
        return await ReadAsync<PaginatedItems<AuditEntryDto>>(response, ct);
    }

    private static async Task<SaveDraftResult> MapSaveAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ThrowIfUnauthorized(response);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK or HttpStatusCode.Created:
                return new SaveDraftResult.Saved(await ReadAsync<RuleVersionDto>(response, ct));
            case HttpStatusCode.BadRequest:
                var validation = await TryReadAsync<HttpValidationProblemDetails>(response, ct);
                if (validation is null)
                {
                    break;
                }

                return new SaveDraftResult.Invalid(validation.Errors.ToDictionary(e => e.Key, e => e.Value));
            case HttpStatusCode.Conflict:
                return new SaveDraftResult.Conflict(await ReadDetailAsync(response, ct));
            case HttpStatusCode.NotFound:
                return new SaveDraftResult.NotFound();
        }

        throw Unexpected(response);
    }

    private static async Task<PublishResult> MapPublishAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ThrowIfUnauthorized(response);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                return new PublishResult.Published((await ReadAsync<PublishResponse>(response, ct)).SnapshotVersion);
            case HttpStatusCode.NotFound:
                return new PublishResult.NotFound();
            case HttpStatusCode.Forbidden:
                return new PublishResult.Forbidden();
            case HttpStatusCode.Conflict:
                var conflict = await TryReadAsync<ProblemDetails>(response, ct);
                var detail = conflict?.Detail ?? conflict?.Title ?? string.Empty;
                return conflict?.Type switch
                {
                    ProblemTypes.V1ClientsActive => new PublishResult.V1ClientsActive(detail),
                    ProblemTypes.ConcurrentPublish => new PublishResult.ConcurrentPublish(),
                    _ => new PublishResult.Conflict(detail),
                };
            case HttpStatusCode.ServiceUnavailable:
                if ((await TryReadAsync<ProblemDetails>(response, ct))?.Type == ProblemTypes.PublishGuardUnavailable)
                {
                    return new PublishResult.GuardUnavailable();
                }

                break;
            case HttpStatusCode.BadRequest:
                var badRequest = await TryReadAsync<ProblemDetails>(response, ct);
                if (badRequest?.Type == ProblemTypes.InvalidGuardOverrideReason)
                {
                    return new PublishResult.InvalidOverrideReason(badRequest.Detail ?? badRequest.Title ?? string.Empty);
                }

                break;
        }

        throw Unexpected(response);
    }

    private static void ThrowIfUnauthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ReauthenticationRequiredException();
        }
    }

    private static HttpRequestException Unexpected(HttpResponseMessage response)
        => new($"BusinessRules.API returned an unexpected status {(int)response.StatusCode}.", inner: null, response.StatusCode);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        ThrowIfUnauthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            throw Unexpected(response);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
            ?? throw new HttpRequestException("BusinessRules.API returned an empty response body.");
    }

    // An unreadable error body is treated as "no problem details" rather than hiding the status.
    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var problem = await TryReadAsync<ProblemDetails>(response, ct);
        return problem?.Detail ?? problem?.Title ?? string.Empty;
    }

    private sealed record PublishRequest(string? GuardOverrideReason);

    private sealed record PublishResponse(Guid RuleVersionId, long SnapshotVersion);
}
