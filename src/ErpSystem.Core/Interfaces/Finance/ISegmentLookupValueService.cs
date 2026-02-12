// FILE: src/ErpSystem.Core/Services/Finance/ISegmentLookupValueService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Services.Finance
{
    /// <summary>
    /// Service contract for managing Segment Lookup Values.
    /// 
    /// RESPONSIBILITIES:
    /// - Maintain dictionary values for segments that use lookup tables
    ///   (e.g., Departments, Cost Centers, Projects).
    /// - Enforce multi-tenancy and per-segment business rules.
    /// - Provide filtered lists for dropdowns and configuration UIs.
    /// </summary>
    public interface ISegmentLookupValueService
    {
        /// <summary>
        /// Gets all lookup values for a given segment structure in the current tenant.
        /// 
        /// USAGE:
        /// - Populate dropdowns for a specific segment (e.g. Department list).
        /// - Admin configuration screens.
        /// </summary>
        Task<IReadOnlyList<SegmentLookupValueDto>> GetBySegmentStructureAsync(
            Guid segmentStructureId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single lookup value by Id.
        /// </summary>
        Task<SegmentLookupValueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new lookup value for a segment in the current tenant.
        /// 
        /// BUSINESS RULES:
        /// - Verify that the segment supports lookup values (LookupTableRequired = true).
        /// - Enforce uniqueness of SegmentValue per segment.
        /// - Enforce length and data type per SegmentLength and DataType.
        /// </summary>
        Task<SegmentLookupValueDto> CreateAsync(
            SegmentLookupValueCreateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing lookup value.
        /// 
        /// RULES:
        /// - Service layer may restrict changes if the value is already used in accounts.
        /// - Description and DisplayOrder are usually safe to change.
        /// </summary>
        Task<SegmentLookupValueDto> UpdateAsync(
            SegmentLookupValueUpdateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes or deactivates a lookup value.
        /// 
        /// NOTES:
        /// - Recommended behavior is to mark as inactive rather than hard delete.
        /// - System should prevent using inactive values in new accounts.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
