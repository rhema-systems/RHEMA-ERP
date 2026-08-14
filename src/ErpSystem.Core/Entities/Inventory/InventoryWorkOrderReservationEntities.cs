using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Inventory;

/// <summary>
/// Append-only lifecycle evidence for a maintenance work-order reservation.
/// InventoryAllocation remains the authoritative reservation and stock owner.
/// </summary>
public sealed class InventoryWorkOrderReservationAction : TenantEntity
{
    [Required] public Guid InventoryAllocationId { get; set; }
    [Required] public Guid WorkOrderPartId { get; set; }
    public int Sequence { get; set; }
    [Required, MaxLength(30)] public string ActionType { get; set; } = string.Empty;
    [MaxLength(20)] public string? PreviousStatus { get; set; }
    [Required, MaxLength(20)] public string NewStatus { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime? PreviousRequiredDate { get; set; }
    public DateTime? NewRequiredDate { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Reason { get; set; }
    [Required] public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? PreviousHash { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryAllocation InventoryAllocation { get; set; } = null!;
    public ErpSystem.Core.Entities.Maintenance.WorkOrderPart WorkOrderPart { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
