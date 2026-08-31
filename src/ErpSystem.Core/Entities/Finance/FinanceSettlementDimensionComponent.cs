using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Immutable-capable allocation evidence for Finance-owned settlements. A row retains one
/// component of one stable settlement source line against one exact originating economic line.
/// This deliberately does not add dimension columns to AP, AR, or Cash module entities.
/// </summary>
public sealed class FinanceSettlementDimensionComponent : TenantEntity
{
    public FinanceDimensionRouteId RouteId { get; set; }

    [Required, MaxLength(50)]
    public string ProducerModule { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string SourceRoute { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ContractVersion { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }

    /// <summary>
    /// Stable Finance-owned economic-line id. For AP/AR this is the payment-allocation id;
    /// advances use their own stable line id. Direct-cash offsets and transfer legs use their
    /// persisted CashTransaction ids.
    /// </summary>
    public Guid SettlementSourceLineId { get; set; }

    public Guid? SettlementAllocationId { get; set; }
    public Guid? OriginatingDocumentId { get; set; }
    public Guid? OriginatingSourceLineId { get; set; }
    public FinanceSettlementComponentType ComponentType { get; set; }

    public Guid? FinanceDimensionSetId { get; set; }
    public Guid? FinanceDimensionSnapshotId { get; set; }

    [Required, MaxLength(3)]
    public string TransactionCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransactionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FunctionalAmount { get; set; }

    public Guid? ExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Required, MaxLength(20)]
    public string EvidenceVersion { get; set; } = "1.0";

    /// <summary>
    /// The final originating line receives the difference between the exact component total and
    /// independently rounded proportional shares. Both values make that deterministic choice
    /// reproducible without changing the accounting total.
    /// </summary>
    public bool IsFinalResidualRecipient { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RoundingResidualTransactionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RoundingResidualFunctionalAmount { get; set; }

    [Required, MaxLength(64)]
    public string EvidenceHash { get; set; } = string.Empty;

    public DateTime? EvidenceFrozenAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey(nameof(FinanceDimensionSetId))]
    public FinanceDimensionSet? FinanceDimensionSet { get; set; }

    [ForeignKey(nameof(FinanceDimensionSnapshotId))]
    public FinanceDimensionSnapshot? FinanceDimensionSnapshot { get; set; }
}
