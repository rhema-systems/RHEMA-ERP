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
            .Include(p => p.OrganizationUnit)
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
        // EmployeePosition no longer links directly to Department in the current model.
        // Best-effort mapping:
        // 1) If an OrganizationUnit exists with Code == Department.Code, return positions in that unit.
        // 2) Fallback: return positions currently assigned to employees in that department.

        var departmentCode = await _context.Set<Department>()
            .Where(d => d.Id == departmentId && !d.IsDeleted)
            .Select(d => d.Code)
            .FirstOrDefaultAsync();

        var query = _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            query = query.Where(p => p.OrganizationUnit.Code == departmentCode);
        }
        else
        {
            query = query.Where(p => p.Employees.Any(e => !e.IsDeleted && e.DepartmentId == departmentId));
        }

        return await query
            .Include(p => p.OrganizationUnit)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Get position by code
    /// </summary>
    public async Task<EmployeePosition?> GetByCodeAsync(string code)
    {
        return await _context.Set<EmployeePosition>()
            .Include(p => p.OrganizationUnit)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Code == code);
    }

    /// <summary>
    /// Get position with skill requirements
    /// </summary>
    public async Task<EmployeePosition?> GetWithSkillRequirementsAsync(Guid id)
    {
        return await _context.Set<EmployeePosition>()
            .Include(p => p.OrganizationUnit)
            .Include(p => p.StaffLevel)
            .Include(p => p.SalaryGrade)
            .Include(p => p.ReportsToPosition)
            .Include(p => p.SkillRequirements)
                .ThenInclude(r => r.Skill)
            .Include(p => p.PositionBenefits)
                .ThenInclude(b => b.BenefitPolicy)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Id == id);
    }

    /// <summary>
    /// Get positions by organizational level
    /// </summary>
    public async Task<IEnumerable<EmployeePosition>> GetByLevelAsync(int level)
    {
        return await _context.Set<EmployeePosition>()
            .Where(p => !p.IsDeleted && p.Level == level)
            .Include(p => p.OrganizationUnit)
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

    /// <inheritdoc />
    public void TrackBenefit(EmployeePositionBenefit benefit)
    {
        _context.Set<EmployeePositionBenefit>().Add(benefit);
    }

    /// <inheritdoc />
    public void TrackSkillRequirement(PositionSkillRequirement requirement)
    {
        _context.Set<PositionSkillRequirement>().Add(requirement);
    }
}