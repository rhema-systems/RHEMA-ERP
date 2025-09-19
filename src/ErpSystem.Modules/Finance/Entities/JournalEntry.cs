using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Modules.Finance.Entities
{
    public class JournalEntry : BaseEntity
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

        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        public JournalStatus Status { get; set; } = JournalStatus.Draft;

        public bool IsReversed { get; set; } = false;
        public Guid? ReversalJournalId { get; set; }
        public JournalEntry? ReversalJournal { get; set; }

        public DateTime? PostedDate { get; set; }
        public Guid? PostedByUserId { get; set; }

        public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();
    }

    public enum JournalStatus
    {
        Draft = 1,
        Posted = 2,
        Reversed = 3
    }
}