using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryValuationReconciliationStatus
{
    Exception = 0,
    Reconciled = 1,
    Frozen = 2
}

public enum InventoryValuationReconciliationActionType
{
    Generated = 0,
    Frozen = 1
}

public sealed class InventoryValuationReconciliation : TenantEntity
{
    [Required, MaxLength(50)]
    public string ReconciliationNumber { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public DateTime CutoffDateUtc { get; set; }
    public InventoryValuationReconciliationStatus Status { get; set; }
    [Required, MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    public Guid InventoryControlAccountId { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceiptInventoryValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal PostedLandedCostValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedCostInventoryValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedCostVarianceValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal InventorySubledgerValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal InventoryBalanceCacheValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentMovementValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal GeneralLedgerValue { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ReconciliationVariance { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ToleranceAmount { get; set; }
    public int ReceiptExceptionCount { get; set; }
    public int LandedCostExceptionCount { get; set; }
    public int ValuationExceptionCount { get; set; }
    public int GeneralLedgerExceptionCount { get; set; }
    public int ExceptionCount { get; set; }
    [Required]
    public string SnapshotJson { get; set; } = "{}";
    [Required]
    public string ExceptionsJson { get; set; } = "[]";
    [Required, MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;
    public Guid GeneratedById { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public Guid? FrozenById { get; set; }
    public DateTime? FrozenAtUtc { get; set; }
    public Guid? PeriodModuleLockId { get; set; }
    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public FiscalPeriod FiscalPeriod { get; set; } = null!;
    public Account InventoryControlAccount { get; set; } = null!;
    public PeriodModuleLock? PeriodModuleLock { get; set; }
    public ICollection<InventoryValuationReconciliationAction> Actions { get; set; } =
        new List<InventoryValuationReconciliationAction>();
}

public sealed class InventoryValuationReconciliationAction : TenantEntity
{
    public Guid ReconciliationId { get; set; }
    public int Sequence { get; set; }
    public InventoryValuationReconciliationActionType ActionType { get; set; }
    public InventoryValuationReconciliationStatus? PreviousStatus { get; set; }
    public InventoryValuationReconciliationStatus NewStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Reason { get; set; }
    [MaxLength(64)]
    public string? PreviousHash { get; set; }
    [Required, MaxLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public InventoryValuationReconciliation Reconciliation { get; set; } = null!;
}
