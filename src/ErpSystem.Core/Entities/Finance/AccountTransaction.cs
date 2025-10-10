using System;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance
{
    public class AccountTransaction : BaseEntity
    {
        [Required]
        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;

        [Required]
        public Guid JournalEntryId { get; set; }
        public JournalEntry JournalEntry { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        public TransactionType TransactionType { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime TransactionDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;

        public decimal BalanceAfter { get; set; }
    }

    public enum TransactionType
    {
        Debit = 1,
        Credit = 2
    }
}