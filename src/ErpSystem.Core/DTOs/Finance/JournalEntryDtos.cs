using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    public class JournalEntryDto
    {
        public Guid Id { get; set; }
        public string JournalNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string JournalType { get; set; } = "General";
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string Status { get; set; } = "Draft";
        /// <summary>Alias for Status — used by approval workflow endpoints.</summary>
        public string PostingStatus { get => Status; set => Status = value; }
        public bool IsReversed { get; set; }
        public Guid? ReversalJournalId { get; set; }
        public Guid? OriginalJournalId { get; set; }
        public DateTime? ReversalDate { get; set; }
        public string? ReversalReason { get; set; }
        public string? ReversalType { get; set; }
        public DateTime? PostedDate { get; set; }
        public Guid? PostedByUserId { get; set; }
        public string? PostedByUserName { get; set; }
        // Approval workflow fields
        public bool RequiresApproval { get; set; }
        public string? ApprovalStatus { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? RejectionReason { get; set; }
        public bool HasAttachments { get; set; }
        public int AttachmentCount { get; set; }
        public List<Guid> AttachmentIds { get; set; } = new();
        public List<AccountTransactionDto> Transactions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class CreateJournalEntryDto
    {
        [MaxLength(50)]
        public string? JournalNumber { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        [MaxLength(50)]
        public string? JournalType { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(20)]
        public string? BookClassification { get; set; }

        [MaxLength(50)]
        public string? SourceModule { get; set; }

        public Guid? SourceDocumentId { get; set; }

        [MaxLength(100)]
        public string? SourceDocumentType { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
        
        public Guid? FiscalPeriodId { get; set; }
        
        public List<Guid> AttachmentIds { get; set; } = new();

        public List<CreateAccountTransactionDto> Transactions { get; set; } = new();
    }

    public class UpdateJournalEntryDto
    {
        public DateTime? TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(20)]
        public string? BookClassification { get; set; }
        
        public List<Guid>? AttachmentIds { get; set; }

        public List<CreateAccountTransactionDto>? Transactions { get; set; }
    }

    /// <summary>
    /// DTO for approval and rejection actions on journal entries.
    /// </summary>
    public class ApprovalActionDto
    {
        /// <summary>Optional comments for approval, required reason for rejection.</summary>
        public string? Comments { get; set; }
        /// <summary>Rejection reason (required when rejecting).</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// DTO for reversing a posted journal entry.
    /// </summary>
    public class ReverseJournalEntryDto
    {
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime? ReversalDate { get; set; }
    }

    public class JournalEntryAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid JournalEntryId { get; set; }
        public Guid FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }
}
