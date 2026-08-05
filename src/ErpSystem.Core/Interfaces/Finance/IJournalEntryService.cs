using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Journal Entries.
    /// Handles general ledger journal entry creation, posting, and reversal.
    /// </summary>
    public interface IJournalEntryService
    {
        /// <summary>
        /// Retrieves all journal entries (alias for GetAllJournalEntriesAsync).
        /// </summary>
        Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single journal entry by ID.
        /// </summary>
        Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a journal entry by journal number.
        /// </summary>
        Task<JournalEntryDto?> GetJournalEntryByNumberAsync(string journalNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves journal entries within a date range.
        /// </summary>
        Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new journal entry.
        /// </summary>
        Task<JournalEntryDto> CreateJournalEntryAsync(CreateJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing journal entry.
        /// Can only update draft entries.
        /// </summary>
        Task<JournalEntryDto> UpdateJournalEntryAsync(Guid id, UpdateJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a journal entry (soft delete).
        /// Can only delete draft entries.
        /// </summary>
        Task DeleteJournalEntryAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a journal entry to the general ledger.
        /// Updates account balances and locks the entry.
        /// </summary>
        Task<JournalEntryDto> PostJournalEntryAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a journal entry inside a caller-owned batch transaction.
        /// The caller notifies the owner only after that outer transaction commits.
        /// </summary>
        Task<JournalEntryDto> PostJournalEntryForBatchAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the owner notification for a successfully committed journal posting.
        /// </summary>
        Task NotifyJournalPostedAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses a posted journal entry.
        /// Creates a reversing entry with opposite debits/credits.
        /// </summary>
        Task<JournalEntryDto> ReverseJournalEntryAsync(Guid id, string reason, DateTime? reversalDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a journal entry is balanced (debits = credits).
        /// </summary>
        Task<bool> ValidateBalanceAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a draft journal entry before it is submitted into the approval workflow.
        /// </summary>
        Task ValidateJournalEntryReadyForSubmissionAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates the next available journal entry number (JE-YYYY-XXXX).
        /// </summary>
        Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the approval/posting status of a journal entry.
        /// Used by the approval workflow endpoints.
        /// </summary>
        Task UpdateApprovalStatusAsync(
            Guid id,
            string postingStatus,
            string approvalStatus,
            Guid? approvedByUserId = null,
            string? rejectionReason = null,
            CancellationToken cancellationToken = default);

        Task LinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default);
        Task UnlinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> GetAttachmentIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<JournalEntryAttachmentDto>> GetAttachmentsAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    }
}
