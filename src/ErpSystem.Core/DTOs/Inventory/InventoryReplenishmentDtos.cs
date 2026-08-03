using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryReplenishmentRecommendationDto
{
    public Guid Id { get; set; }
    public string RecommendationNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public InventoryReplenishmentRecommendationStatus Status { get; set; }
    public int DemandWindowDays { get; set; }
    public DateTime DemandFromUtc { get; set; }
    public DateTime DemandToUtc { get; set; }
    public decimal DemandQuantity { get; set; }
    public decimal AverageDailyDemand { get; set; }
    public int LeadTimeDays { get; set; }
    public int SafetyLeadTimeDays { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal AllocatedStock { get; set; }
    public decimal OnOrderQuantity { get; set; }
    public decimal OpenRecommendationQuantity { get; set; }
    public decimal MinimumLevel { get; set; }
    public decimal MaximumLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal OrderMultiple { get; set; }
    public decimal LeadTimeDemand { get; set; }
    public decimal ProjectedAvailableAtReceipt { get; set; }
    public decimal RecommendedQuantity { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public DateTime RequiredDateUtc { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string CalculationHash { get; set; } = string.Empty;
    public Guid GeneratedById { get; set; }
    public string GeneratedByName { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public string? PurchaseRequisitionNumber { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<InventoryReplenishmentActionDto> Actions { get; set; } = new();
}

public sealed class InventoryReplenishmentActionDto
{
    public int Sequence { get; set; }
    public InventoryReplenishmentActionType ActionType { get; set; }
    public InventoryReplenishmentRecommendationStatus? PreviousStatus { get; set; }
    public InventoryReplenishmentRecommendationStatus NewStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class GenerateInventoryReplenishmentRequest
{
    public Guid WarehouseId { get; set; }
    public int DemandWindowDays { get; set; } = 90;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class SubmitInventoryReplenishmentRequest
{
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideInventoryReplenishmentRequest
{
    public bool Approved { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ConvertInventoryReplenishmentRequest
{
    public string Department { get; set; } = "Stores";
    public string? CostCenter { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
