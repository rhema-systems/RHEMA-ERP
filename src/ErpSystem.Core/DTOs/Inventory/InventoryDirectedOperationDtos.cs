using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryDirectedAssigneeDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class InventoryLocationCapacityDto
{
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPickingLocation { get; set; }
    public bool IsReceivingLocation { get; set; }
    public bool IsQuarantineLocation { get; set; }
    public decimal UsedWeight { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal UsedVolume { get; set; }
    public decimal? MaxVolume { get; set; }
    public int UsedItemSlots { get; set; }
    public int? MaxItemSlots { get; set; }
    public decimal IncomingWeight { get; set; }
    public decimal IncomingVolume { get; set; }
    public int IncomingItemSlots { get; set; }
    public bool HasCapacity { get; set; }
    public List<string> CapacityIssues { get; set; } = new();
}

public sealed class InventoryDirectedSuggestionDto
{
    public string SuggestionKey { get; set; } = string.Empty;
    public InventoryDirectedTaskType TaskType { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? SourceLocationId { get; set; }
    public string? SourceLocationCode { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? DestinationLocationCode { get; set; }
    public decimal Quantity { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid SourceLineId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool IsQuarantine { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public InventoryLocationCapacityDto? DestinationCapacity { get; set; }
}

public sealed class CreateInventoryDirectedTaskRequest
{
    public Guid WarehouseId { get; set; }
    [Required, StringLength(64, MinimumLength = 64)] public string SuggestionKey { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class StartInventoryDirectedTaskRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
}

public sealed class ConfirmInventoryDirectedTaskRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [StringLength(100)] public string? LotNumber { get; set; }
    [StringLength(100)] public string? BatchNumber { get; set; }
    [StringLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
}

public sealed class CancelInventoryDirectedTaskRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class ReconcileInventoryDirectedTaskRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class InventoryDirectedTaskActionDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public InventoryDirectedTaskActionType ActionType { get; set; }
    public InventoryDirectedTaskStatus StatusAfter { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class InventoryDirectedTaskDto
{
    public Guid Id { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public InventoryDirectedTaskType TaskType { get; set; }
    public InventoryDirectedTaskStatus Status { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? SourceLocationId { get; set; }
    public string? SourceLocationCode { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? DestinationLocationCode { get; set; }
    public decimal Quantity { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid SourceLineId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool IsQuarantine { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public Guid? LinkedInventoryTransferId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<InventoryDirectedTaskActionDto> Actions { get; set; } = new();
}
