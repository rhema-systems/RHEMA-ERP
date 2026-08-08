using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Benefit Policy operations.
///
/// This service is intended to be consumed by API controllers and UI clients.
/// All operations are tenant-scoped via the application's current tenant context.
/// </summary>
public interface IBenefitPolicyService
{
    /// <summary>
    /// Gets a benefit policy by identifier.
    /// Returns <c>null</c> when not found.
    /// </summary>
    Task<BenefitPolicyDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all benefit policies.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicyDto>> GetAllAsync();

    /// <summary>
    /// Gets a paged list of benefit policies.
    /// </summary>
    Task<PagedResult<BenefitPolicyListDto>> GetPagedAsync(int page, int pageSize);

    /// <summary>
    /// Gets all active benefit policies.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicyDto>> GetAllActiveAsync();

    /// <summary>
    /// Gets benefit policies by policy type.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicyDto>> GetByPolicyTypeAsync(BenefitPolicyType policyType);

    /// <summary>
    /// Gets benefit policies effective as of the provided date.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicyDto>> GetEffectivePoliciesAsync(DateTime asOfDate);

    /// <summary>
    /// Creates a new benefit policy.
    /// </summary>
    Task<BenefitPolicyDto> CreateAsync(CreateBenefitPolicyDto dto);

    /// <summary>
    /// Updates an existing benefit policy.
    /// </summary>
    Task<BenefitPolicyDto> UpdateAsync(Guid id, UpdateBenefitPolicyDto dto);

    /// <summary>
    /// Deactivates (soft deactivation) a benefit policy.
    /// Returns <c>false</c> when not found.
    /// </summary>
    Task<bool> DeactivateAsync(Guid id);

    /// <summary>
    /// Soft deletes a benefit policy.
    /// Returns <c>false</c> when not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Gets the enum option sets (delivery types, tax treatments, valuation methods, etc.)
    /// used to drive benefit-policy configuration UIs.
    /// </summary>
    Task<BenefitPolicyLookupsDto> GetLookupsAsync();

    /// <summary>Gets the per-grade value rows for a policy.</summary>
    Task<IReadOnlyList<BenefitGradeValueDto>> GetGradeValuesAsync(Guid policyId);

    /// <summary>Adds a per-grade value row to a policy.</summary>
    Task<BenefitGradeValueDto> AddGradeValueAsync(Guid policyId, CreateBenefitGradeValueDto dto);

    /// <summary>Updates a per-grade value row on a policy.</summary>
    Task<BenefitGradeValueDto> UpdateGradeValueAsync(Guid policyId, Guid gradeValueId, UpdateBenefitGradeValueDto dto);

    /// <summary>Deletes a per-grade value row from a policy. Returns <c>false</c> when not found.</summary>
    Task<bool> DeleteGradeValueAsync(Guid policyId, Guid gradeValueId);
}
