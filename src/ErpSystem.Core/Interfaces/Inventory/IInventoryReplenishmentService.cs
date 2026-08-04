using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryReplenishmentService
{
    Task<IReadOnlyList<InventoryReplenishmentRecommendationDto>> GetAsync(
        Guid? warehouseId, InventoryReplenishmentRecommendationStatus? status, int take,
        CancellationToken cancellationToken = default);
    Task<InventoryReplenishmentRecommendationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReplenishmentRecommendationDto>> GenerateAsync(
        GenerateInventoryReplenishmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryReplenishmentRecommendationDto> SubmitAsync(
        Guid id, SubmitInventoryReplenishmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryReplenishmentRecommendationDto> DecideAsync(
        Guid id, DecideInventoryReplenishmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryReplenishmentRecommendationDto> ConvertToPurchaseRequisitionAsync(
        Guid id, ConvertInventoryReplenishmentRequest request, CancellationToken cancellationToken = default);
}

public class InventoryReplenishmentControlException : Exception
{
    public InventoryReplenishmentControlException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class InventoryReplenishmentAuthorizationException : InventoryReplenishmentControlException
{
    public InventoryReplenishmentAuthorizationException(string message)
        : base("INV_REPLENISHMENT_FORBIDDEN", message) { }
}

public sealed class InventoryReplenishmentNotFoundException : InventoryReplenishmentControlException
{
    public InventoryReplenishmentNotFoundException(string message)
        : base("INV_REPLENISHMENT_NOT_FOUND", message) { }
}
