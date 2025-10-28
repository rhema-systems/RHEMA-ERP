using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technical skill operations
/// </summary>
public class TechnicalSkillRepository : GenericRepository<TechnicalSkill>, ITechnicalSkillRepository
{
    public TechnicalSkillRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TechnicalSkill>> GetActiveAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.IsActive && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetByCategoryAsync(string category)
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.Category == category && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetBySkillLevelAsync(string skillLevel)
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.SkillLevel == skillLevel && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetByComplexityAsync(string complexity)
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.Complexity == complexity && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetByRiskLevelAsync(string riskLevel)
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.RiskLevel == riskLevel && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<TechnicalSkill?> GetByCodeAsync(string code)
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.Code == code && !ts.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        var query = _context.TechnicalSkills
            .Where(ts => ts.Code == code && !ts.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(ts => ts.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetFromHRModuleAsync()
    {
        // TODO: Implement actual HR API integration
        // For now, return empty collection until HR integration is implemented
        await Task.CompletedTask;
        return new List<TechnicalSkill>();
    }

    public async Task<IEnumerable<TechnicalSkill>> SyncFromHRAsync()
    {
        var hrSkills = await GetFromHRModuleAsync();
        var syncedSkills = new List<TechnicalSkill>();

        foreach (var hrSkill in hrSkills)
        {
            var existingSkill = await GetByCodeAsync(hrSkill.Code);
            if (existingSkill != null)
            {
                // Update existing skill
                existingSkill.Name = hrSkill.Name;
                existingSkill.Description = hrSkill.Description;
                existingSkill.Category = hrSkill.Category;
                existingSkill.SkillLevel = hrSkill.SkillLevel;
                existingSkill.Complexity = hrSkill.Complexity;
                existingSkill.RiskLevel = hrSkill.RiskLevel;
                existingSkill.LastSyncDate = DateTime.UtcNow;
                existingSkill.IsFromHRModule = true;
                
                await UpdateAsync(existingSkill);
                syncedSkills.Add(existingSkill);
            }
            else
            {
                // Add new skill from HR
                hrSkill.TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default tenant
                hrSkill.LastSyncDate = DateTime.UtcNow;
                await AddAsync(hrSkill);
                syncedSkills.Add(hrSkill);
            }
        }

        await _context.SaveChangesAsync();
        return syncedSkills;
    }

    public async Task<Dictionary<string, int>> GetSkillCategoryCountsAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => !ts.IsDeleted)
            .GroupBy(ts => ts.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetSkillLevelCountsAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => !ts.IsDeleted)
            .GroupBy(ts => ts.SkillLevel)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetComplexityCountsAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => !ts.IsDeleted)
            .GroupBy(ts => ts.Complexity)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<IEnumerable<TechnicalSkill>> GetSkillsRequiringSyncAsync(int daysSinceLastSync = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastSync);
        
        return await _context.TechnicalSkills
            .Where(ts => ts.IsFromHRModule && 
                        (ts.LastSyncDate == null || ts.LastSyncDate < cutoffDate) &&
                        !ts.IsDeleted)
            .OrderBy(ts => ts.LastSyncDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetLocalSkillsAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => !ts.IsFromHRModule && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicalSkill>> GetHRSyncedSkillsAsync()
    {
        return await _context.TechnicalSkills
            .Where(ts => ts.IsFromHRModule && !ts.IsDeleted)
            .OrderBy(ts => ts.Name)
            .ToListAsync();
    }
    
    public async Task<bool> IsSkillNameUniqueAsync(string name, Guid? excludeId = null)
    {
        var query = _context.TechnicalSkills
            .Where(ts => ts.Name == name && !ts.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(ts => ts.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }
    
    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null)
    {
        return await IsSkillNameUniqueAsync(name, excludeId);
    }
    
    public async Task<int> GetTechnicianCountBySkillAsync(Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.SkillId == skillId && !tsa.IsDeleted)
            .CountAsync();
    }
    
    public async Task<IEnumerable<TechnicalSkill>> GetBySkillLevelAsync(int skillLevel)
    {
        // Convert int skill level to string representation for query
        string skillLevelString = skillLevel switch
        {
            1 => "Beginner",
            2 => "Intermediate", 
            3 => "Advanced",
            4 => "Expert",
            _ => "Intermediate" // Default fallback
        };
        
        return await GetBySkillLevelAsync(skillLevelString);
    }
}
