using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Repository interface for MaintenanceTool operations
/// </summary>
public interface IMaintenanceToolRepository
{
    /// <summary>
    /// Gets a tool by ID
    /// </summary>
    Task<MaintenanceTool?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all active tools
    /// </summary>
    Task<List<MaintenanceTool>> GetAllAsync();

    /// <summary>
    /// Gets available tools (status = Available)
    /// </summary>
    Task<List<MaintenanceTool>> GetAvailableToolsAsync();

    /// <summary>
    /// Gets tools by status
    /// </summary>
    Task<List<MaintenanceTool>> GetByStatusAsync(string status);

    /// <summary>
    /// Gets tools by category
    /// </summary>
    Task<List<MaintenanceTool>> GetByCategoryAsync(string category);

    /// <summary>
    /// Gets a tool by tool code
    /// </summary>
    Task<MaintenanceTool?> GetByToolCodeAsync(string toolCode);

    /// <summary>
    /// Adds a new tool
    /// </summary>
    Task AddAsync(MaintenanceTool tool);

    /// <summary>
    /// Updates an existing tool
    /// </summary>
    Task UpdateAsync(MaintenanceTool tool);

    /// <summary>
    /// Deletes a tool
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Checks if tool code exists
    /// </summary>
    Task<bool> ToolCodeExistsAsync(string toolCode, Guid? excludeId = null);
}
