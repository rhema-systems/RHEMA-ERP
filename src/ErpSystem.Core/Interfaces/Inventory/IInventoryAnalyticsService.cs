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

/// <summary>
/// Internal full-result source used by the shared statutory report engine. The public
/// analytics endpoint retains its bounded take contract.
/// </summary>
public interface IInventoryAnalyticsReportSource
{
    Task<InventoryAnalyticsDto> GetReportSourceAsync(
        Guid? warehouseId,
        Guid? categoryId,
        int slowMovingDays,
        int nonMovingDays,
        int expiryWarningDays,
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
