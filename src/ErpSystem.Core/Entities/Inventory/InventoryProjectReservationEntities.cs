using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

public sealed class InventoryProjectReservationAction : TenantEntity
{
    public Guid InventoryAllocationId { get; set; }
    public int Sequence { get; set; }
    public InventoryProjectReservationActionType ActionType { get; set; }
    public InventoryProjectReservationStatus? PreviousStatus { get; set; }
    public InventoryProjectReservationStatus NewStatus { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    public Guid? PreviousInventoryItemId { get; set; }
    public Guid? NewInventoryItemId { get; set; }
    public Guid? NotificationId { get; set; }
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

    public InventoryAllocation InventoryAllocation { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
    public Notification? Notification { get; set; }
}
