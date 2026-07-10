namespace ErpSystem.Core.DTOs.Maintenance;

public sealed class MaintenanceOperationalReportsDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public List<MaintenanceAssetMovementReportRowDto> AssetMovements { get; set; } = new();
    public List<MaintenanceInspectionServiceReportRowDto> InspectionServiceHistory { get; set; } = new();
    public List<MaintenanceWorkOrderStatusReportRowDto> WorkOrderStatus { get; set; } = new();
    public List<MaintenancePartIssuedReportRowDto> PartsIssued { get; set; } = new();
    public List<MaintenanceWorkOrderCostReportRowDto> CostsByWorkOrder { get; set; } = new();
    public List<MaintenanceAssetCostReportRowDto> CostsByAsset { get; set; } = new();
}

public sealed class MaintenanceAssetMovementReportRowDto
{
    public Guid MovementId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public DateTime EffectiveAtUtc { get; set; }
    public string FromProject { get; set; } = string.Empty;
    public string FromSite { get; set; } = string.Empty;
    public string ToProject { get; set; } = string.Empty;
    public string ToSite { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class MaintenanceInspectionServiceReportRowDto
{
    public Guid RecordId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string SheetType { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string InspectionKind { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class MaintenanceWorkOrderStatusReportRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string WorkOrderType { get; set; } = string.Empty;
    public string MaintenanceType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssignedTechnician { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RequestedCompletionAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsOverdue { get; set; }
}

public sealed class MaintenancePartIssuedReportRowDto
{
    public Guid WorkOrderPartId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityIssued { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
}

public sealed class MaintenanceWorkOrderCostReportRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal PartsCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal TotalCost { get; set; }
}

public sealed class MaintenanceAssetCostReportRowDto
{
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public int WorkOrderCount { get; set; }
    public decimal PartsCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal TotalCost { get; set; }
}
