using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Resolves the exact policy that governs a Draft requisition once category,
/// currency, and estimated value are known. Selection remains explicit when
/// more than one Published policy is eligible.
/// </summary>
public sealed class ProcurementRequisitionPolicySelectionService(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PurchaseRequisitionPolicyOptionDto>> GetEligibleAsync(
        Guid tenantId,
        ProcurementCategoryClass category,
        decimal amount,
        string currencyCode,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        var currency = NormalizeCurrency(currencyCode);
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new ProcurementRequisitionPolicySelectionException(
                "PR_POLICY_CURRENCY_INVALID", "A three-letter transaction currency is required before policy selection.");

        var policies = await unitOfWork.Repository<ProcurementPolicySet>()
            .GetQueryable(value =>
                value.TenantId == tenantId &&
                value.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                value.EffectiveFrom <= atUtc &&
                (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= atUtc))
            .AsNoTracking()
            .OrderBy(value => value.Code)
            .ThenByDescending(value => value.Version)
            .ToListAsync(cancellationToken);
        if (policies.Count == 0) return [];

        var policyIds = policies.Select(value => value.Id).ToList();
        var categoryRules = await unitOfWork.Repository<ProcurementPolicyCategoryRule>()
            .GetQueryable(value =>
                value.TenantId == tenantId && policyIds.Contains(value.PolicySetId) &&
                value.IsEnabled && value.Category == category &&
                value.EffectiveFrom <= atUtc && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= atUtc))
            .AsNoTracking().ToListAsync(cancellationToken);
        var methodRules = await unitOfWork.Repository<ProcurementPolicyMethodRule>()
            .GetQueryable(value =>
                value.TenantId == tenantId && policyIds.Contains(value.PolicySetId) &&
                value.IsEnabled && value.IsAllowed && value.Category == category &&
                value.EffectiveFrom <= atUtc && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= atUtc))
            .AsNoTracking().ToListAsync(cancellationToken);
        var thresholdRules = await unitOfWork.Repository<ProcurementPolicyThresholdRule>()
            .GetQueryable(value =>
                value.TenantId == tenantId && policyIds.Contains(value.PolicySetId) &&
                value.IsEnabled && value.Category == category && value.CurrencyCode == currency &&
                value.EffectiveFrom <= atUtc && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= atUtc))
            .AsNoTracking().ToListAsync(cancellationToken);

        var results = new List<PurchaseRequisitionPolicyOptionDto>();
        foreach (var policy in policies)
        {
            var policyCategories = categoryRules.Where(value => value.PolicySetId == policy.Id).ToList();
            if (policyCategories.Count == 0) continue;
            var policyMethods = methodRules.Where(value => value.PolicySetId == policy.Id).ToList();
            foreach (var threshold in thresholdRules.Where(value =>
                         value.PolicySetId == policy.Id && AmountMatches(value, amount)))
            {
                var method = policyMethods
                    .Where(value => value.Method == threshold.Method &&
                                    ServiceClassesMatch(value.ServiceClass, threshold.ServiceClass) &&
                                    policyCategories.Any(categoryRule => ServiceClassesMatch(categoryRule.ServiceClass, threshold.ServiceClass)))
                    .OrderByDescending(value => value.Priority)
                    .ThenBy(value => value.RuleCode)
                    .FirstOrDefault();
                if (method is null) continue;
                results.Add(new PurchaseRequisitionPolicyOptionDto
                {
                    PolicySetId = policy.Id,
                    PolicyCode = policy.Code,
                    PolicyName = policy.Name,
                    PolicyVersion = policy.Version,
                    Category = category,
                    Method = method.Method,
                    CurrencyCode = currency,
                    LowerBound = threshold.LowerBound,
                    UpperBound = threshold.UpperBound,
                    LowerInclusive = threshold.LowerInclusive,
                    UpperInclusive = threshold.UpperInclusive,
                    ServiceClass = threshold.ServiceClass
                });
            }
        }

        return results
            .GroupBy(value => value.PolicySetId)
            .Select(group => group.OrderBy(value => value.LowerBound).First())
            .OrderBy(value => value.PolicyCode)
            .ToList();
    }

    public async Task<ProcurementPolicySet?> ResolveAsync(
        Guid tenantId,
        Guid? requestedPolicySetId,
        ProcurementCategoryClass? category,
        decimal amount,
        string currencyCode,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        var effective = await unitOfWork.Repository<ProcurementPolicySet>()
            .GetQueryable(value =>
                value.TenantId == tenantId &&
                value.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                value.EffectiveFrom <= atUtc &&
                (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= atUtc))
            .AsNoTracking().ToListAsync(cancellationToken);

        if (!category.HasValue)
        {
            if (requestedPolicySetId.HasValue)
                throw new ProcurementRequisitionPolicySelectionException(
                    "PR_POLICY_CATEGORY_REQUIRED", "Select a procurement category before choosing a policy.");
            return effective.Count == 1 ? effective[0] : null;
        }

        var eligible = await GetEligibleAsync(
            tenantId, category.Value, amount, currencyCode, atUtc, cancellationToken);
        if (requestedPolicySetId.HasValue)
        {
            if (eligible.All(value => value.PolicySetId != requestedPolicySetId.Value))
                throw new ProcurementRequisitionPolicySelectionException(
                    "PR_POLICY_NOT_ELIGIBLE",
                    "The selected policy is not Published, effective, tenant-scoped, or applicable to this requisition category, currency, and value.");
            return effective.Single(value => value.Id == requestedPolicySetId.Value);
        }

        if (eligible.Count == 1)
            return effective.Single(value => value.Id == eligible[0].PolicySetId);
        if (eligible.Count > 1)
            throw new ProcurementRequisitionPolicySelectionException(
                "PR_POLICY_SELECTION_REQUIRED",
                "More than one Published policy applies. Select the exact policy for this requisition.");
        if (effective.Count == 1)
            return effective[0];
        if (effective.Count > 1)
            throw new ProcurementRequisitionPolicySelectionException(
                "PR_POLICY_NOT_FOUND",
                "No Published policy matches this requisition category, currency, and value.");
        return null;
    }

    private static bool AmountMatches(ProcurementPolicyThresholdRule rule, decimal amount) =>
        (rule.LowerInclusive ? amount >= rule.LowerBound : amount > rule.LowerBound) &&
        (!rule.UpperBound.HasValue ||
         (rule.UpperInclusive ? amount <= rule.UpperBound.Value : amount < rule.UpperBound.Value));

    private static bool ServiceClassesMatch(string? left, string? right) =>
        string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right) ||
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}

public sealed class ProcurementRequisitionPolicySelectionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
