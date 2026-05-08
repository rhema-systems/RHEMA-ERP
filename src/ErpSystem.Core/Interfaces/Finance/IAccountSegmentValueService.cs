// FILE: src/ErpSystem.Core/Services/Finance/IAccountSegmentValueService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Services.Finance
{
    /// <summary>
    /// Service contract for managing Account Segment Values.
    /// 
    /// RESPONSIBILITIES:
    /// - Manage the per-account segment assignments (Company, Dept, Cost Center, etc.).
    /// - Provide read APIs for displaying the segment breakdown of an account.
    /// - Support controlled updates where business rules allow.
    /// 
    /// NOTE:
    /// - In many implementations, segment values are primarily managed via the AccountService
    ///   (during account create/update) and this service is used for advanced admin operations.
    /// </summary>
    public interface IAccountSegmentValueService
    {
        /// <summary>
        /// Retrieves all segment values for a given account, ordered by SegmentPosition.
        /// </summary>
        Task<IReadOnlyList<AccountSegmentValueDto>> GetByAccountAsync(
            Guid accountId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single segment value by Id.
        /// </summary>
        Task<AccountSegmentValueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new segment value for an account.
        /// 
        /// BUSINESS RULES:
        /// - Enforce that the account exists and belongs to current tenant.
        /// - Enforce that SegmentStructureId is valid for that tenant.
        /// - Enforce SegmentPosition uniqueness per account.
        /// - Enforce length/DataType and lookup rules.
        /// - May only be allowed before any postings exist for that account.
        /// </summary>
        Task<AccountSegmentValueDto> CreateAsync(
            AccountSegmentValueCreateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing segment value for an account.
        /// 
        /// BUSINESS RULES:
        /// - If IsLocked is true or account has postings, changes may be blocked
        ///   or heavily restricted.
        /// - Service must keep historical integrity of postings and reports.
        /// </summary>
        Task<AccountSegmentValueDto> UpdateAsync(
            AccountSegmentValueUpdateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes (or logically disables) a segment value for an account.
        /// 
        /// NOTES:
        /// - Typically only allowed if account has no postings or if the segment
        ///   is being replaced in a controlled migration.
        /// - Implementation may choose to set EndDate rather than hard delete.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
