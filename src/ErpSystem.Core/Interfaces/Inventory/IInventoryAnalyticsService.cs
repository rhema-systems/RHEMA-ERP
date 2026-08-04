using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryAnalyticsService
{
    Task<InventoryAnalyticsDto> GetAsync(
        Guid? warehouseId,
        Guid? categoryId,
        int slowMovingDays,
        int nonMovingDays,
        int expiryWarningDays,
        int take,
        CancellationToken cancellationToken = default);
}

public class InventoryAnalyticsException : Exception
{
    public InventoryAnalyticsException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class InventoryAnalyticsAuthorizationException : InventoryAnalyticsException
{
    public InventoryAnalyticsAuthorizationException(string message)
        : base("INV_ANALYTICS_FORBIDDEN", message) { }
}
