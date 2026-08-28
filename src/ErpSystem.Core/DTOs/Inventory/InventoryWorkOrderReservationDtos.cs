namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryWorkOrderReservationActionDto
{
    public Guid Id { get; init; }
    public Guid InventoryAllocationId { get; init; }
    public Guid WorkOrderPartId { get; init; }
    public int Sequence { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string? PreviousStatus { get; init; }
    public string NewStatus { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public DateTime? PreviousRequiredDate { get; init; }
    public DateTime? NewRequiredDate { get; init; }
    public string? Reason { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

public sealed class InventoryWorkOrderReservationResultDto
{
    public Guid WorkOrderId { get; init; }
    public int AffectedParts { get; init; }
    public DateTime? RequiredDate { get; init; }
    public bool Replayed { get; init; }
}
