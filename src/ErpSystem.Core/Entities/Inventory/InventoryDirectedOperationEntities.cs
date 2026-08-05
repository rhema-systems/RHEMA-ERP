using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryDirectedTaskType
{
    PutAway = 1,
    Picking = 2,
    Replenishment = 3
}

public enum InventoryDirectedTaskStatus
{
    Assigned = 1,
    InProgress = 2,
    AwaitingStockMove = 3,
    Completed = 4,
    Cancelled = 5
}

public enum InventoryDirectedTaskActionType
{
    Created = 1,
    Started = 2,
    PickConfirmed = 3,
    PlacementConfirmed = 4,
    TransferCreated = 5,
    TransferCompleted = 6,
    Cancelled = 7
}

[Table("InventoryDirectedTasks")]
public sealed class InventoryDirectedTask : TenantEntity
{
    [Required, MaxLength(50)] public string TaskNumber { get; set; } = string.Empty;
    public InventoryDirectedTaskType TaskType { get; set; }
    public InventoryDirectedTaskStatus Status { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid? SourceLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Required, MaxLength(100)] public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid SourceLineId { get; set; }
    [Required, MaxLength(100)] public string SourceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string SuggestionKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string CapacitySnapshotJson { get; set; } = "{}";
    public bool IsQuarantine { get; set; }
    public Guid AssignedToUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? StartedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public Guid? LinkedInventoryTransferId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Notes { get; set; }
    [MaxLength(1000)] public string? CancellationReason { get; set; }
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Warehouse Warehouse { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public WarehouseLocation? SourceLocation { get; set; }
    public WarehouseLocation? DestinationLocation { get; set; }
    public ApplicationUser AssignedToUser { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public InventoryTransfer? LinkedInventoryTransfer { get; set; }
    public ICollection<InventoryDirectedTaskAction> Actions { get; set; } = new List<InventoryDirectedTaskAction>();
}

[Table("InventoryDirectedTaskActions")]
public sealed class InventoryDirectedTaskAction : TenantEntity
{
    public Guid TaskId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public InventoryDirectedTaskActionType ActionType { get; set; }
    public InventoryDirectedTaskStatus StatusAfter { get; set; }
    public Guid ActorUserId { get; set; }
    [Required, MaxLength(300)] public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string PayloadJson { get; set; } = "{}";
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryDirectedTask Task { get; set; } = null!;
}
