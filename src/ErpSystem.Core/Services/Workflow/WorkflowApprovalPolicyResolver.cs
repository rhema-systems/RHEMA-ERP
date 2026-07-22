using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;

namespace ErpSystem.Core.Services.Workflow;

public sealed record WorkflowApprovalPolicyContext(
    Guid TenantId,
    string EntityType,
    DateTime EffectiveAt,
    string? Module = null,
    string? Category = null,
    Guid? LocationId = null,
    Guid? LegalEntityId = null,
    decimal? Amount = null,
    string? CurrencyCode = null);

public sealed record WorkflowApprovalPolicyResolution(
    Guid PolicySetId,
    string PolicyCode,
    WorkflowApprovalConfigDto ApprovalConfig);

public interface IWorkflowApprovalPolicyResolver
{
    Task<WorkflowApprovalPolicyResolution?> ResolveAsync(
        WorkflowApprovalPolicyContext context,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowApprovalPolicyResolver : IWorkflowApprovalPolicyResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IWorkflowApprovalPolicySetRepository _repository;

    public WorkflowApprovalPolicyResolver(IWorkflowApprovalPolicySetRepository repository)
    {
        _repository = repository;
    }

    public async Task<WorkflowApprovalPolicyResolution?> ResolveAsync(
        WorkflowApprovalPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _repository.GetEffectiveCandidatesAsync(
            context.TenantId,
            context.EntityType,
            context.EffectiveAt,
            cancellationToken);
        var match = WorkflowApprovalPolicyMatcher.Select(candidates, context);
        if (match == null)
        {
            return null;
        }

        var config = JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(match.ApprovalConfiguration, JsonOptions)
            ?? throw new InvalidOperationException($"Approval policy '{match.Code}' has invalid configuration.");
        return new WorkflowApprovalPolicyResolution(match.Id, match.Code, config);
    }
}

public static class WorkflowApprovalPolicyMatcher
{
    public static WorkflowApprovalPolicySet? Select(
        IEnumerable<WorkflowApprovalPolicySet> candidates,
        WorkflowApprovalPolicyContext context)
    {
        return candidates
            .Where(policy => Matches(policy, context))
            .OrderByDescending(policy => policy.Priority)
            .ThenByDescending(Specificity)
            .ThenByDescending(policy => policy.EffectiveFrom)
            .FirstOrDefault();
    }

    private static bool Matches(WorkflowApprovalPolicySet policy, WorkflowApprovalPolicyContext context)
    {
        return MatchText(policy.EntityType, context.EntityType) &&
            MatchText(policy.Module, context.Module) &&
            MatchText(policy.Category, context.Category) &&
            (!policy.LocationId.HasValue || policy.LocationId == context.LocationId) &&
            (!policy.LegalEntityId.HasValue || policy.LegalEntityId == context.LegalEntityId) &&
            (!policy.MinimumAmount.HasValue || context.Amount >= policy.MinimumAmount) &&
            (!policy.MaximumAmount.HasValue || context.Amount <= policy.MaximumAmount) &&
            MatchText(policy.CurrencyCode, context.CurrencyCode);
    }

    private static bool MatchText(string? policyValue, string? contextValue)
        => string.IsNullOrWhiteSpace(policyValue) ||
            string.Equals(policyValue.Trim(), contextValue?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static int Specificity(WorkflowApprovalPolicySet policy)
        => new object?[]
        {
            policy.Module, policy.EntityType, policy.Category, policy.LocationId, policy.LegalEntityId,
            policy.MinimumAmount, policy.MaximumAmount, policy.CurrencyCode
        }.Count(value => value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        });
}
