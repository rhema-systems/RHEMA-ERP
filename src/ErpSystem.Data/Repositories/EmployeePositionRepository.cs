using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for employee position operations
/// </summary>
public class EmployeePositionRepository : GenericRepository<EmployeePosition>, IEmployeePositionRepository
{
    public EmployeePositionRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get all active positions
    /// </summary>
    public async Task<IEnumerable<EmployeePosition>> GetActivePositionsAsync()
    {
        return await _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Include(p => p.Department)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Get positions by organization unit
    /// </summary>
    public async Task<IEnumerable<EmployeePosition>> GetByOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted && p.OrganizationUnitId == organizationUnitId)
            .Include(p => p.OrganizationUnit)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Get positions by department
    /// </summary>
    public async Task<IEnumerable<EmployeePosition>> GetByDepartmentAsync(Guid departmentId)
    {
        return await _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted && p.DepartmentId == departmentId)
            .Include(p => p.Department)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Get position by code
    /// </summary>
    public async Task<EmployeePosition?> GetByCodeAsync(string code)
    {
        return await _context.Set<EmployeePosition>()
            .Include(p => p.Department)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Code == code);
    }

    /// <summary>
    /// Get position with skill requirements
    /// </summary>
    public async Task<EmployeePosition?> GetWithSkillRequirementsAsync(Guid id)
    {
        return await _context.Set<EmployeePosition>()
            .Include(p => p.Department)
            .Include(p => p.SkillRequirements)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Id == id);
    }

    /// <summary>
    /// Get positions by organizational level
    /// </summary>
    public async Task<IEnumerable<EmployeePosition>> GetByLevelAsync(int level)
    {
        return await _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted && p.Level == level)
            .Include(p => p.Department)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Check if position code exists
    /// </summary>
    public async Task<bool> CodeExistsAsync(string code)
    {
        return await _context.Set<EmployeePosition>()
            .AnyAsync(p => !p.IsDeleted && p.Code == code);
    }
}
