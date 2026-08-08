using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryProjectReservationService
{
    Task<IReadOnlyList<InventoryProjectReservationDto>> GetAsync(
        Guid? projectId = null,
        Guid? departmentId = null,
        InventoryProjectReservationStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationDto> ReserveAsync(CreateInventoryProjectReservationRequest request, CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationDto> ReleaseAsync(Guid id, ReleaseInventoryProjectReservationRequest request, CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationDto> SubstituteAsync(Guid id, SubstituteInventoryProjectReservationRequest request, CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationFulfillmentResult> FulfillForIssueAsync(InventoryProjectReservationFulfillmentRequest request, CancellationToken cancellationToken = default);
    Task ReleaseForCancelledRequisitionAsync(Guid requisitionId, Guid actorUserId, string reason, string correlationId, CancellationToken cancellationToken = default);
    Task<InventoryProjectReservationExpiryResult> ExpireDueAsync(Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed class InventoryProjectReservationControlException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryProjectReservationAuthorizationException(string message) : UnauthorizedAccessException(message);
public sealed class InventoryProjectReservationNotFoundException(string message) : KeyNotFoundException(message);
