using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public class OpeningBalanceBatch : BusinessEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SourceReference { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime OpeningDate { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod FiscalPeriod { get; set; } = null!;

    [MaxLength(30)]
    public string BookClassification { get; set; } = "IFRS";

    [MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference { get; set; }

    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? ValidatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    public ICollection<OpeningBalanceLine> Lines { get; set; } = new List<OpeningBalanceLine>();
}

public class OpeningBalanceLine : TenantEntity
{
    public Guid OpeningBalanceBatchId { get; set; }
    public OpeningBalanceBatch Batch { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    [MaxLength(3)]
    public string TransactionCurrencyCode { get; set; } = "GHS";

    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    public Guid? ExchangeRateId { get; set; }
    public ExchangeRate? ExchangeRate { get; set; }

    public DateTime? ExchangeRateDate { get; set; }

    [MaxLength(500)]
    public string? SegmentString { get; set; }

    public Guid? BankAccountId { get; set; }

    [MaxLength(30)]
    public string? CounterpartyType { get; set; }

    public Guid? CounterpartyId { get; set; }

    [MaxLength(100)]
    public string? SourceReference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
