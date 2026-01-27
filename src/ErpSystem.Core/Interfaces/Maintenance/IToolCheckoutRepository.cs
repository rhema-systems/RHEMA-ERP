using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Repository interface for ToolCheckout operations
/// </summary>
public interface IToolCheckoutRepository
{
    /// <summary>
    /// Gets a checkout by ID with full details
    /// </summary>
    Task<ToolCheckout?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets active checkouts (not returned)
    /// </summary>
    Task<List<ToolCheckout>> GetActiveCheckoutsAsync(Guid? employeeId = null);

    /// <summary>
    /// Gets overdue checkouts (expected return date passed, not returned)
    /// </summary>
    Task<List<ToolCheckout>> GetOverdueCheckoutsAsync();

    /// <summary>
    /// Gets checkout history for a specific tool
    /// </summary>
    Task<List<ToolCheckout>> GetToolCheckoutHistoryAsync(Guid toolId, int limit = 50);

    /// <summary>
    /// Gets checkout history for a specific employee
    /// </summary>
    Task<List<ToolCheckout>> GetEmployeeCheckoutHistoryAsync(Guid employeeId, int limit = 50);

    /// <summary>
    /// Gets checkouts by work order
    /// </summary>
    Task<List<ToolCheckout>> GetByWorkOrderAsync(Guid workOrderId);

    /// <summary>
    /// Gets active checkout for a specific tool
    /// </summary>
    Task<ToolCheckout?> GetActiveCheckoutByToolIdAsync(Guid toolId);

    /// <summary>
    /// Adds a new checkout
    /// </summary>
    Task AddAsync(ToolCheckout checkout);

    /// <summary>
    /// Updates an existing checkout
    /// </summary>
    Task UpdateAsync(ToolCheckout checkout);

    /// <summary>
    /// Gets total usage days for a tool
    /// </summary>
    Task<int> GetTotalUsageDaysAsync(Guid toolId);

    /// <summary>
    /// Gets total checkout count for a tool
    /// </summary>
    Task<int> GetTotalCheckoutCountAsync(Guid toolId);
}
