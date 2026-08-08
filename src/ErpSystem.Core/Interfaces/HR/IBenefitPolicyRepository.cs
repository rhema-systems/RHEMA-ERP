using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Benefit policy repository.
/// Extends the generic repository with BenefitPolicy-specific queries.
/// </summary>
public interface IBenefitPolicyRepository : IGenericRepository<BenefitPolicy>
{
    /// <summary>
    /// Gets a benefit policy by id including its related collections.
    /// </summary>
    Task<BenefitPolicy?> GetByIdWithRelationsAsync(Guid id);

    /// <summary>
    /// Gets all active benefit policies.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicy>> GetAllActiveAsync();

    /// <summary>
    /// Gets benefit policies by policy type.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicy>> GetByTypeAsync(BenefitPolicyType policyType);

    /// <summary>
    /// Checks whether a policy code already exists.
    /// Optionally excludes an existing record (used during updates).
    /// </summary>
    Task<bool> PolicyCodeExistsAsync(string policyCode, Guid? excludeId = null);

    /// <summary>
    /// Gets benefit policies that are effective on the provided date.
    /// </summary>
    Task<IReadOnlyList<BenefitPolicy>> GetEffectivePoliciesAsync(DateTime asOfDate);
}
