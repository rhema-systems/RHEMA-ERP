// FILE: src/ErpSystem.Core/Services/Finance/IAccountSegmentStructureService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Account Segment Structures.
    /// 
    /// RESPONSIBILITIES:
    /// - Define and maintain the *shape* of the segmented Chart of Accounts
    ///   (segment name, code, position, length, reporting flags, etc.).
    /// - Ensure structural integrity across all segments for a tenant
    ///   (e.g., no duplicate positions, consistent natural account designation).
    /// </summary>
    public interface IAccountSegmentStructureService
    {
        /// <summary>
        /// Gets all segment structures for the current tenant, ordered by SegmentPosition.
        /// </summary>
        Task<IReadOnlyList<AccountSegmentStructureDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single segment structure by its Id.
        /// </summary>
        Task<AccountSegmentStructureDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new segment structure for the current tenant.
        /// 
        /// RULES:
        /// - Enforce unique SegmentCode per tenant.
        /// - Enforce unique SegmentPosition per tenant.
        /// - Validate SegmentLength bounds and consistency.
        /// - Ensure only one segment is marked as natural account.
        /// </summary>
        Task<AccountSegmentStructureDto> CreateAsync(
            AccountSegmentStructureCreateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing segment structure.
        /// 
        /// RULES:
        /// - May restrict changes when accounts already exist using this segment.
        /// - Service should set CanBeModified and RestrictionWarning appropriately
        ///   in the returned DTO.
        /// </summary>
        Task<AccountSegmentStructureDto> UpdateAsync(
            AccountSegmentStructureUpdateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reorders segments according to the provided list of new positions.
        /// 
        /// PROCESS:
        /// 1. Validate new positions (must be sequential 1..N, no duplicates).
        /// 2. Update SegmentStructure entities.
        /// 3. Update existing AccountSegmentValue positions.
        /// 4. Regenerate AccountNumber for all affected accounts.
        /// </summary>
        Task ReorderSegmentsAsync(List<ReorderSegmentDto> reorderList, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes (or logically disables) a segment structure.
        /// 
        /// NOTES:
        /// - Implementation should consider whether any accounts currently depend on this segment.
        /// - May switch IsActive to false instead of deleting when dependencies exist.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forces regeneration of all account numbers based on current segment structure and positions.
        /// </summary>
        Task RegenerateAccountNumbersAsync(CancellationToken cancellationToken = default);
    }
}
