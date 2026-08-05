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

        /// <summary>
        /// Get controlled allocation run batches.
        /// </summary>
        Task<IReadOnlyList<AllocationRunBatchDto>> GetRunBatchesAsync(string? status = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a controlled allocation run batch by ID.
        /// </summary>
        Task<AllocationRunBatchDto?> GetRunBatchByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate and persist a draft allocation run batch for approval.
        /// </summary>
        Task<AllocationRunBatchDto> CreateRunBatchAsync(CreateAllocationRunBatchDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Submit a draft allocation run batch to workflow approval.
        /// </summary>
        Task<AllocationRunBatchDto> SubmitRunBatchAsync(Guid id, string? comment = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Approve the current workflow step for an allocation run batch.
        /// </summary>
        Task<AllocationRunBatchDto> ApproveRunBatchAsync(Guid id, string? comment = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reject the current workflow step for an allocation run batch.
        /// </summary>
        Task<AllocationRunBatchDto> RejectRunBatchAsync(Guid id, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Post an approved allocation run batch to GL.
        /// </summary>
        Task<AllocationRunBatchDto> PostRunBatchAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
