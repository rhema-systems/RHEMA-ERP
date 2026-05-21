using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Service interface for tool checkout operations
/// </summary>
public interface IToolCheckoutService
{
    /// <summary>
    /// Checkout a tool to an employee
    /// </summary>
    Task<ToolCheckoutResult> CheckoutToolAsync(Guid toolId, Guid employeeId, CheckoutToolDto dto);

    /// <summary>
    /// Return a checked-out tool
    /// </summary>
    Task<ToolReturnResult> ReturnToolAsync(Guid checkoutId, Guid returnedByEmployeeId, ReturnToolDto dto);

    /// <summary>
    /// Get active checkouts for an employee or all active checkouts
    /// </summary>
    Task<List<ToolCheckoutDto>> GetActiveCheckoutsAsync(Guid? employeeId = null);

    /// <summary>
    /// Get overdue checkouts
    /// </summary>
    Task<List<ToolCheckoutDto>> GetOverdueCheckoutsAsync();

    /// <summary>
    /// Check tool availability for a given date range
    /// </summary>
    Task<ToolAvailabilityDto> CheckToolAvailabilityAsync(Guid toolId, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get tool checkout history
    /// </summary>
    Task<ToolCheckoutHistoryDto> GetToolCheckoutHistoryAsync(Guid toolId, int limit = 50);

    /// <summary>
    /// Get available tools
    /// </summary>
    Task<List<MaintenanceToolDto>> GetAvailableToolsAsync();

    /// <summary>
    /// Report damage for a checked-out tool
    /// </summary>
    Task ReportToolDamageAsync(Guid checkoutId, ToolDamageDto dto);

    /// <summary>
    /// Get tool details by ID
    /// </summary>
    Task<MaintenanceToolDto?> GetToolByIdAsync(Guid toolId);

    /// <summary>
    /// Get all tools
    /// </summary>
    Task<List<MaintenanceToolDto>> GetAllToolsAsync();

    /// <summary>
    /// Get employee checkout history
    /// </summary>
    Task<List<ToolCheckoutDto>> GetEmployeeCheckoutHistoryAsync(Guid employeeId, int limit = 50);
}
