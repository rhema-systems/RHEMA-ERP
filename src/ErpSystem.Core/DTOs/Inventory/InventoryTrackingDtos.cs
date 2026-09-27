using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryTrackingRequirementsDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool RequiresLot { get; set; }
    public bool RequiresBatch { get; set; }
    public bool RequiresSerial { get; set; }
    public bool RequiresManufactureDate { get; set; }
    public bool RequiresExpiryDate { get; set; }
    public bool EnforcesFifoIssue { get; set; }
    public int MinimumShelfLifeDays { get; set; }
    public List<Guid> CategoryLineage { get; set; } = new();
}

public sealed class InventoryTrackingMutationRequest
{
    // Internal typed authority; callers cannot opt into transit writes through JSON.
    [System.Text.Json.Serialization.JsonIgnore] public Guid? TransferDispatchAllocationId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public Guid? TransferReceiptAllocationId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public InventoryTrackingDirection Direction { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? TrackingExceptionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class RegisterInventoryTrackingExceptionRequest
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    [Required, MaxLength(100)] public string ReferenceType { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, MinLength(1)] public List<string> ExceptionCodes { get; set; } = new();
    [Required, MinLength(10), MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowEvidenceDocumentId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class InventoryTrackingExceptionDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public List<string> ExceptionCodes { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowEvidenceDocumentId { get; set; }
    public Guid ApprovedById { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class InventoryTraceabilityEventDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? TrackingExceptionId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
