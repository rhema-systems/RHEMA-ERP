using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service interface for Allocation Rule operations.
    /// </summary>
    public interface IAllocationService
    {
        /// <summary>
        /// Get all allocation rules for the current tenant.
        /// </summary>
        Task<IReadOnlyList<AllocationRuleDto>> GetAllRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get active allocation rules only.
        /// </summary>
        Task<IReadOnlyList<AllocationRuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific allocation rule by ID.
        /// </summary>
        Task<AllocationRuleDto?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific allocation rule by code.
        /// </summary>
        Task<AllocationRuleDto?> GetRuleByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new allocation rule.
        /// </summary>
        Task<AllocationRuleDto> CreateRuleAsync(CreateAllocationRuleDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing allocation rule.
        /// </summary>
        Task<AllocationRuleDto> UpdateRuleAsync(Guid id, UpdateAllocationRuleDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete an allocation rule.
        /// </summary>
        Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activate an allocation rule.
        /// </summary>
        Task<AllocationRuleDto> ActivateRuleAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivate an allocation rule.
        /// </summary>
        Task<AllocationRuleDto> DeactivateRuleAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Run an allocation for the specified rule and period.
        /// </summary>
        Task<AllocationResultDto> RunAllocationAsync(RunAllocationDto dto, CancellationToken cancellationToken = default);
    }
}
