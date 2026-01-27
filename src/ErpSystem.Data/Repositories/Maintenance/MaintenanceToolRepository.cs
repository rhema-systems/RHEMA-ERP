using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class MaintenanceToolRepository : IMaintenanceToolRepository
{
    private readonly ApplicationDbContext _context;

    public MaintenanceToolRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MaintenanceTool?> GetByIdAsync(Guid id)
    {
        return await _context.MaintenanceTools
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<MaintenanceTool>> GetAllAsync()
    {
        return await _context.MaintenanceTools
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<MaintenanceTool>> GetAvailableToolsAsync()
    {
        return await _context.MaintenanceTools
            .Where(t => t.IsActive && t.Status == "Available")
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<MaintenanceTool>> GetByStatusAsync(string status)
    {
        return await _context.MaintenanceTools
            .Where(t => t.IsActive && t.Status == status)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<MaintenanceTool>> GetByCategoryAsync(string category)
    {
        return await _context.MaintenanceTools
            .Where(t => t.IsActive && t.Category == category)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<MaintenanceTool?> GetByToolCodeAsync(string toolCode)
    {
        return await _context.MaintenanceTools
            .FirstOrDefaultAsync(t => t.ToolCode == toolCode);
    }

    public async Task AddAsync(MaintenanceTool tool)
    {
        await _context.MaintenanceTools.AddAsync(tool);
    }

    public Task UpdateAsync(MaintenanceTool tool)
    {
        var entry = _context.Entry(tool);

        if (entry.State == EntityState.Detached)
        {
            _context.MaintenanceTools.Attach(tool);
            entry.State = EntityState.Modified;
        }
        else
        {
            _context.MaintenanceTools.Update(tool);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var tool = await GetByIdAsync(id);
        if (tool != null)
        {
            tool.IsActive = false;
            await UpdateAsync(tool);
        }
    }

    public async Task<bool> ToolCodeExistsAsync(string toolCode, Guid? excludeId = null)
    {
        var query = _context.MaintenanceTools.Where(t => t.ToolCode == toolCode);

        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
