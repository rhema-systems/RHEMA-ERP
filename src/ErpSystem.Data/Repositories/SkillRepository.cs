using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for skill operations.
/// </summary>
public sealed class SkillRepository : GenericRepository<Skill>, ISkillRepository
{
    public SkillRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Skill>> GetActiveSkillsAsync()
    {
        return await _context.Set<Skill>()
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Skill>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return new List<Skill>();
        }

        return await _context.Set<Skill>()
            .Where(s => !s.IsDeleted && s.Category == category)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Skill>> GetCertificationRequiredSkillsAsync()
    {
        return await _context.Set<Skill>()
            .Where(s => !s.IsDeleted && s.RequiresCertification)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Skill?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return await _context.Set<Skill>()
            .FirstOrDefaultAsync(s => !s.IsDeleted && s.Name == name);
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return await _context.Set<Skill>()
            .AnyAsync(s => !s.IsDeleted && s.Name == name);
    }
}
