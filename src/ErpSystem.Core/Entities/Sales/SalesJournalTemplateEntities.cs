using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Sales
{
    /// <summary>
    /// Sales journal template — configurable GL posting rules for sales transactions
    /// </summary>
    public class SalesJournalTemplate : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Transaction type this template applies to (SalesOrder, CreditNote, Refund, etc.)
        /// </summary>
        [Required, MaxLength(100)]
        public string TransactionType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PostingBehavior { get; set; }

        public bool AutoPost { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<SalesJournalTemplateLine> Lines { get; set; } = new List<SalesJournalTemplateLine>();
    }

    /// <summary>
    /// Individual line within a journal template — defines GL account and amount source
    /// </summary>
    public class SalesJournalTemplateLine : BaseEntity
    {
        public Guid SalesJournalTemplateId { get; set; }
        public SalesJournalTemplate SalesJournalTemplate { get; set; } = null!;

        public int Sequence { get; set; }

        [MaxLength(100)]
        public string AccountType { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? AccountCode { get; set; }

        [MaxLength(200)]
        public string? AccountName { get; set; }

        [MaxLength(10)]
        public string EntryType { get; set; } = "Debit";

        [MaxLength(100)]
        public string AmountSource { get; set; } = "TotalAmount";

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FixedAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Percentage { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
