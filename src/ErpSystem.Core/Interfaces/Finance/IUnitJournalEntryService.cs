using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Unit Journal Entries.
    /// Handles creation, workflow (approval/rejection), posting, and reversal
    /// of journal entries that record unit quantity changes.
    /// </summary>
    public interface IUnitJournalEntryService
    {
        /// <summary>
        /// Retrieves all unit journal entries for the current tenant.
        /// </summary>
        Task<IReadOnlyList<UnitJournalEntryDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves unit journal entries filtered by status, date range, etc.
        /// </summary>
        Task<IReadOnlyList<UnitJournalEntryDto>> GetFilteredAsync(
            UnitJournalEntryFilters filters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves entries pending approval.
        /// </summary>
        Task<IReadOnlyList<UnitJournalEntryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single unit journal entry by ID with all lines.
        /// </summary>
        Task<UnitJournalEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a unit journal entry by entry number.
        /// </summary>
        Task<UnitJournalEntryDto?> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new unit journal entry in Draft status.
        /// </summary>
        Task<UnitJournalEntryDto> CreateAsync(CreateUnitJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing draft journal entry.
        /// </summary>
        Task<UnitJournalEntryDto> UpdateAsync(Guid id, UpdateUnitJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a draft journal entry (soft delete).
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Submits a draft entry for approval. Changes status to PendingApproval.
        /// </summary>
        Task<UnitJournalEntryDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Approves a pending entry. Changes status to Approved.
        /// </summary>
        Task<UnitJournalEntryDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rejects a pending entry. Changes status to Rejected.
        /// </summary>
        Task<UnitJournalEntryDto> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts an approved entry. Updates unit account balances and changes status to Posted.
        /// </summary>
        Task<UnitJournalEntryDto> PostAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses a posted entry. Creates a reversing entry with opposite quantities.
        /// </summary>
        Task<UnitJournalEntryDto> ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that journal entry lines are balanced (net zero for balanced entries)
        /// or valid for unit accounts (non-balanced entries allowed for unit accounts).
        /// </summary>
        Task<bool> ValidateEntryAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates the next entry number based on sequence.
        /// </summary>
        Task<string> GenerateEntryNumberAsync(CancellationToken cancellationToken = default);
    }
}
