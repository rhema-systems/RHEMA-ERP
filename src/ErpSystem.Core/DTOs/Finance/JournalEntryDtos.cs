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
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string Status { get; set; } = "Draft";
        /// <summary>Alias for Status — used by approval workflow endpoints.</summary>
        public string PostingStatus { get => Status; set => Status = value; }
        public bool IsReversed { get; set; }
        public Guid? ReversalJournalId { get; set; }
        public DateTime? PostedDate { get; set; }
        public Guid? PostedByUserId { get; set; }
        public string? PostedByUserName { get; set; }
        // Approval workflow fields
        public bool RequiresApproval { get; set; }
        public string? ApprovalStatus { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? RejectionReason { get; set; }
        public List<AccountTransactionDto> Transactions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateJournalEntryDto
    {
        [Required]
        [MaxLength(50)]
        public string JournalNumber { get; set; } = string.Empty;

        [Required]
        public DateTime TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(50)]
        public string? SourceModule { get; set; }
        
        public Guid? FiscalPeriodId { get; set; }

        public List<CreateAccountTransactionDto> Transactions { get; set; } = new();
    }

    public class UpdateJournalEntryDto
    {
        public DateTime? TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

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
}
