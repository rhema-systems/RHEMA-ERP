using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryNegativeStockControlService
{
    Task<InventoryNegativeStockPolicyDto> GetEffectivePolicyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryNegativeStockOverrideDto>> GetOverridesAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<InventoryNegativeStockOverrideDto> RegisterApprovedOverrideAsync(RegisterInventoryNegativeStockOverrideRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<InventoryStockDecreaseAuthorization> PrepareDecreaseAsync(InventoryStockDecreaseRequest request, CancellationToken cancellationToken = default);
    Task ClearMutationContextAsync(CancellationToken cancellationToken = default);
}

public interface IInventoryNegativeStockMutationStore
{
    bool HasRequiredTransaction { get; }
    Task<long> GetCurrentTransactionIdAsync(CancellationToken cancellationToken = default);
    Task SetMutationContextAsync(Guid overrideId, long transactionId, CancellationToken cancellationToken = default);
    Task ClearMutationContextAsync(CancellationToken cancellationToken = default);
}

public sealed class InventoryNegativeStockControlException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryNegativeStockAuthorizationException(string message) : UnauthorizedAccessException(message);
