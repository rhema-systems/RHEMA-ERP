using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryTrackingControlService
{
    Task<InventoryTrackingRequirementsDto> GetRequirementsAsync(Guid inventoryItemId, CancellationToken cancellationToken = default);
    Task ValidateAsync(InventoryTrackingMutationRequest request, CancellationToken cancellationToken = default);
    Task StageEventAsync(InventoryTrackingMutationRequest request, CancellationToken cancellationToken = default);
    Task<InventoryTrackingExceptionDto> RegisterApprovedExceptionAsync(RegisterInventoryTrackingExceptionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTrackingExceptionDto>> GetExceptionsAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTraceabilityEventDto>> GetEventsAsync(Guid? inventoryItemId = null, Guid? warehouseId = null, int take = 200, CancellationToken cancellationToken = default);
}

public sealed class InventoryTrackingControlException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryTrackingAuthorizationException(string message) : UnauthorizedAccessException(message);
