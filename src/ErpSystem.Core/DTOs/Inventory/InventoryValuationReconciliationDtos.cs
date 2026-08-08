using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class GenerateInventoryValuationReconciliationRequest
{
    public Guid FiscalPeriodId { get; set; }
    public decimal ToleranceAmount { get; set; } = 0.01m;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public sealed class FreezeInventoryValuationReconciliationRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public sealed class InventoryValuationReconciliationExceptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
}

public sealed class InventoryValuationReconciliationActionDto
{
    public int Sequence { get; set; }
    public InventoryValuationReconciliationActionType ActionType { get; set; }
    public InventoryValuationReconciliationStatus? PreviousStatus { get; set; }
    public InventoryValuationReconciliationStatus NewStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class InventoryValuationReconciliationDto
{
    public Guid Id { get; set; }
    public string ReconciliationNumber { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodCode { get; set; } = string.Empty;
    public string FiscalPeriodName { get; set; } = string.Empty;
    public bool IsYearEnd { get; set; }
    public DateTime CutoffDateUtc { get; set; }
    public InventoryValuationReconciliationStatus Status { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public Guid InventoryControlAccountId { get; set; }
    public string InventoryControlAccountCode { get; set; } = string.Empty;
    public string InventoryControlAccountName { get; set; } = string.Empty;
    public decimal ReceiptInventoryValue { get; set; }
    public decimal PostedLandedCostValue { get; set; }
    public decimal LandedCostInventoryValue { get; set; }
    public decimal LandedCostVarianceValue { get; set; }
    public decimal InventorySubledgerValue { get; set; }
    public decimal InventoryBalanceCacheValue { get; set; }
    public decimal CurrentMovementValue { get; set; }
    public decimal GeneralLedgerValue { get; set; }
    public decimal ReconciliationVariance { get; set; }
    public decimal ToleranceAmount { get; set; }
    public int ReceiptExceptionCount { get; set; }
    public int LandedCostExceptionCount { get; set; }
    public int ValuationExceptionCount { get; set; }
    public int GeneralLedgerExceptionCount { get; set; }
    public int ExceptionCount { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public Guid GeneratedById { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public Guid? FrozenById { get; set; }
    public DateTime? FrozenAtUtc { get; set; }
    public Guid? PeriodModuleLockId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<InventoryValuationReconciliationExceptionDto> Exceptions { get; set; } =
        Array.Empty<InventoryValuationReconciliationExceptionDto>();
    public IReadOnlyList<InventoryValuationReconciliationActionDto> Actions { get; set; } =
        Array.Empty<InventoryValuationReconciliationActionDto>();
}
