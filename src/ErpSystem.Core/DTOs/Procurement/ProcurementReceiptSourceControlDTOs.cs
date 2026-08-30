using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementReceiptSourceLineRequest
{
    public Guid PurchaseOrderItemId { get; init; }
    public Guid? InventoryItemId { get; init; }
    public Guid? WarehouseId { get; init; }
    public decimal ReceivedQuantity { get; init; }
}

public sealed class ProcurementReceiptSourceLineDto
{
    public Guid PurchaseOrderItemId { get; init; }
    public Guid? InventoryItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal OrderedQuantity { get; init; }
    public decimal PreviouslyReceiptedQuantity { get; init; }
    public decimal ToleranceQuantity { get; init; }
    public decimal MaximumReceivableQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Allowed { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementReceiptSourceReadinessDto
{
    public Guid PurchaseOrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string PurchaseOrderStatus { get; init; } = string.Empty;
    public ProcurementPurchaseOrderSourceType? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string SourceIntegrityHash { get; init; } = string.Empty;
    public decimal TolerancePercent { get; init; }
    public bool SourceValid { get; init; }
    public bool CanReceive { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementReceiptSourceLineDto> Lines { get; init; } =
        Array.Empty<ProcurementReceiptSourceLineDto>();
    public IReadOnlyList<string> RequiredActions { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementReceiptSourceSnapshot
{
    public string SnapshotJson { get; init; } = string.Empty;
    public string IntegrityHash { get; init; } = string.Empty;
    public decimal TolerancePercent { get; init; }
    public DateTime ValidatedAtUtc { get; init; }
    public ProcurementReceiptSourceReadinessDto Readiness { get; init; } = new();
}
