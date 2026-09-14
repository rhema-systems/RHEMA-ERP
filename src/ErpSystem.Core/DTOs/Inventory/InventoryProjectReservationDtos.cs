using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryProjectReservationDto
{
    public Guid Id { get; set; }
    public Guid InventoryRequisitionId { get; set; }
    public Guid InventoryRequisitionItemId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ReservedQuantity { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public InventoryProjectReservationStatus Status { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public Guid ReservedById { get; set; }
    public string ReservedByName { get; set; } = string.Empty;
    public Guid? SubstitutedFromReservationId { get; set; }
    public Guid? SubstitutedByReservationId { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<InventoryProjectReservationActionDto> Actions { get; set; } = new();
    public List<InventoryProjectReservationNotificationDto> Notifications { get; set; } = new();
}

public sealed class InventoryProjectReservationActionDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public InventoryProjectReservationActionType ActionType { get; set; }
    public InventoryProjectReservationStatus? PreviousStatus { get; set; }
    public InventoryProjectReservationStatus NewStatus { get; set; }
    public decimal Quantity { get; set; }
    public Guid? PreviousInventoryItemId { get; set; }
    public Guid? NewInventoryItemId { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class InventoryProjectReservationNotificationDto
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime ScheduledFor { get; set; }
}

public sealed class CreateInventoryProjectReservationRequest
{
    public Guid InventoryRequisitionItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class ReleaseInventoryProjectReservationRequest
{
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SubstituteInventoryProjectReservationRequest
{
    public Guid ReplacementInventoryItemId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class InventoryProjectReservationFulfillmentRequest
{
    public Guid InventoryRequisitionId { get; set; }
    public Guid InventoryRequisitionItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
    public Guid ActorUserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class InventoryProjectReservationFulfillmentResult
{
    public decimal ReservedQuantityApplied { get; set; }
    public Guid? ReservationId { get; set; }
}

public sealed class InventoryProjectReservationExpiryResult
{
    public int ExpiredCount { get; set; }
    public List<Guid> ReservationIds { get; set; } = new();
}
