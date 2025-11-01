using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technical skill operations - reads from HR Skills table
/// </summary>
public class TechnicalSkillRepository : GenericRepository<Skill>, ITechnicalSkillRepository
{
    public TechnicalSkillRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Skill>> GetActiveAsync()
    {
        return await _context.Skills
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Skill>> GetByCategoryAsync(string category)
    {
        return await _context.Skills
            .Where(s => s.Category == category && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Skill?> GetByNameAsync(string name)
    {
        return await _context.Skills
            .Where(s => s.Name == name && !s.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null)
    {
        var query = _context.Skills
            .Where(s => s.Name == name && !s.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }
    
    public async Task<int> GetTechnicianCountBySkillAsync(Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.SkillId == skillId && !tsa.IsDeleted)
            .CountAsync();
    }
}
