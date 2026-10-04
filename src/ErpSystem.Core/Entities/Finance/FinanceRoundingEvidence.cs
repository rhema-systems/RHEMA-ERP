using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Immutable source-to-ledger evidence for a governed invoice or physical-cash rounding decision.
/// The posting idempotency key is the decision identity so a retry reuses the original policy
/// snapshot even when tenant settings, currency precision, exchange rates, or accounts change.
/// </summary>
public sealed class FinanceRoundingEvidence : TenantEntity
{
    [Required, MaxLength(450)]
    public string PostingIdempotencyKey { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string SourceModule { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }

    [Required, MaxLength(50)]
    public string PostingAction { get; set; } = "Post";

    public FinanceRoundingEligibility Eligibility { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public int DecimalPlaces { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal RoundedAmount { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal DeltaAmount { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal Increment { get; set; }

    public GovernedRoundingMethod Method { get; set; }

    [Required, MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = string.Empty;

    public int FunctionalDecimalPlaces { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal OriginalFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal RoundedFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal FunctionalDeltaAmount { get; set; }

    public Guid? ExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,10)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [MaxLength(100)]
    public string? ExchangeRateSource { get; set; }

    public DateTime? ExchangeRateDate { get; set; }

    public Guid GainAccountId { get; set; }

    public Guid LossAccountId { get; set; }
}

public enum FinanceRoundingEligibility
{
    Invoice = 1,
    CashTillTender = 2
}
