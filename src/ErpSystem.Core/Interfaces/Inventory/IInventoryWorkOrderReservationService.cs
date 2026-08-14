using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryWorkOrderReservationService
{
    Task<WorkOrderPartDto> CreateAndReserveAsync(CreateWorkOrderPartDto request, string idempotencyKey,
        string correlationId, CancellationToken cancellationToken = default);
    Task<WorkOrderPartDto> UpdateAsync(Guid partId, UpdateWorkOrderPartDto request, string idempotencyKey,
        string correlationId, CancellationToken cancellationToken = default);
    Task DeleteAndReleaseAsync(Guid partId, string reason, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default);
    Task<WorkOrderPartDto> ReturnUnusedAsync(Guid partId, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default);
    Task<WorkOrderPartDto> RetryAsync(Guid partId, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default);
    Task<InventoryWorkOrderReservationResultDto> ReserveForScheduleAsync(Guid workOrderId, DateTime? requiredDate,
        string idempotencyKey, string correlationId, CancellationToken cancellationToken = default);
    Task<InventoryWorkOrderReservationResultDto> RescheduleAsync(Guid workOrderId, DateTime? requiredDate,
        string reason, string idempotencyKey, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryWorkOrderReservationActionDto>> GetActionsAsync(Guid partId,
        CancellationToken cancellationToken = default);
}

public class InventoryWorkOrderReservationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryWorkOrderReservationAuthorizationException(string message)
    : UnauthorizedAccessException(message);

public sealed class InventoryWorkOrderReservationNotFoundException(string message)
    : KeyNotFoundException(message);
