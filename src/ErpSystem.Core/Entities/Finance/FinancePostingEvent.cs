using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public class FinancePostingEvent : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string SourceModule { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required]
    public Guid SourceDocumentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PostingAction { get; set; } = "Post";

    [MaxLength(100)]
    public string? SourceDocumentReference { get; set; }

    [MaxLength(450)]
    public string? IdempotencyKey { get; set; }

    public Guid? JournalEntryId { get; set; }

    [Required]
    [MaxLength(30)]
    public string PostingStatus { get; set; } = "Pending";

    public DateTime PostingDate { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PostedAt { get; set; }

    public Guid? RequestedByUserId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebitAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCreditAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    public bool HasForeignCurrencyLines { get; set; }

    [MaxLength(3)]
    public string? PrimaryTransactionCurrencyCode { get; set; }

    public Guid? PrimaryExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? PrimaryExchangeRate { get; set; }

    public DateTime? PrimaryExchangeRateDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry? JournalEntry { get; set; }

    [ForeignKey(nameof(PrimaryExchangeRateId))]
    public virtual ExchangeRate? PrimaryExchangeRateRecord { get; set; }
}
