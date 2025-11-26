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
        public bool IsReversed { get; set; }
        public Guid? ReversalJournalId { get; set; }
        public DateTime? PostedDate { get; set; }
        public Guid? PostedByUserId { get; set; }
        public string? PostedByUserName { get; set; }
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

        public List<CreateAccountTransactionDto> Transactions { get; set; } = new();
    }
}
