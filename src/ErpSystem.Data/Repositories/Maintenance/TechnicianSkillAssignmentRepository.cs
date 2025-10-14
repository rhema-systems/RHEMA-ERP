using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technician skill assignment operations
/// </summary>
public class TechnicianSkillAssignmentRepository : GenericRepository<TechnicianSkillAssignment>, ITechnicianSkillAssignmentRepository
{
    public TechnicianSkillAssignmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.TechnicianId == technicianId && !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .OrderBy(tsa => tsa.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetBySkillIdAsync(Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.SkillId == skillId && !tsa.IsDeleted)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.Technician.FirstName)
            .ThenBy(tsa => tsa.Technician.LastName)
            .ToListAsync();
    }

    public async Task<TechnicianSkillAssignment?> GetTechnicianSkillAsync(Guid technicianId, Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.TechnicianId == technicianId && 
                         tsa.SkillId == skillId && 
                         !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetByProficiencyLevelAsync(int proficiencyLevel)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.ProficiencyLevel == proficiencyLevel && !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.Technician.FirstName)
            .ThenBy(tsa => tsa.Technician.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetVerifiedAssignmentsAsync(Guid? technicianId = null)
    {
        var query = _context.TechnicianSkillAssignments
            .Where(tsa => tsa.IsVerified && !tsa.IsDeleted);

        if (technicianId.HasValue)
        {
            query = query.Where(tsa => tsa.TechnicianId == technicianId.Value);
        }

        return await query
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.Technician.FirstName)
            .ThenBy(tsa => tsa.Technician.LastName)
            .ThenBy(tsa => tsa.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetExpiringAssignmentsAsync(DateTime withinDate)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.ExpirationDate.HasValue && 
                         tsa.ExpirationDate.Value <= withinDate && 
                         !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.ExpirationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetExpiredAssignmentsAsync()
    {
        var today = DateTime.UtcNow.Date;
        
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.ExpirationDate.HasValue && 
                         tsa.ExpirationDate.Value < today && 
                         !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.ExpirationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetBySkillCategoryAsync(string category)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.Skill.Category == category && !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.Technician.FirstName)
            .ThenBy(tsa => tsa.Technician.LastName)
            .ToListAsync();
    }

    public async Task<bool> HasSkillAssignmentAsync(Guid technicianId, Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .AnyAsync(tsa => tsa.TechnicianId == technicianId && 
                            tsa.SkillId == skillId && 
                            !tsa.IsDeleted);
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetTechniciansWithSkillAsync(Guid skillId, int? minProficiencyLevel = null)
    {
        var query = _context.TechnicianSkillAssignments
            .Where(tsa => tsa.SkillId == skillId && !tsa.IsDeleted);

        if (minProficiencyLevel.HasValue)
        {
            query = query.Where(tsa => tsa.ProficiencyLevel >= minProficiencyLevel.Value);
        }

        return await query
            .Include(tsa => tsa.Technician)
            .Include(tsa => tsa.Skill)
            .OrderByDescending(tsa => tsa.ProficiencyLevel)
            .ThenBy(tsa => tsa.Technician.FirstName)
            .ThenBy(tsa => tsa.Technician.LastName)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, int>> GetSkillCountsByTechnicianAsync()
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => !tsa.IsDeleted)
            .GroupBy(tsa => tsa.TechnicianId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<Guid, int>> GetTechnicianCountsBySkillAsync()
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => !tsa.IsDeleted)
            .GroupBy(tsa => tsa.SkillId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<int, int>> GetProficiencyLevelCountsAsync()
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => !tsa.IsDeleted)
            .GroupBy(tsa => tsa.ProficiencyLevel)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<decimal> GetAverageProficiencyLevelAsync(Guid? skillId = null)
    {
        var query = _context.TechnicianSkillAssignments
            .Where(tsa => !tsa.IsDeleted);

        if (skillId.HasValue)
        {
            query = query.Where(tsa => tsa.SkillId == skillId.Value);
        }

        return await query.AverageAsync(tsa => (decimal)tsa.ProficiencyLevel);
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetRecentAssignmentsAsync(int daysBack = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
        
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.CreatedAt >= cutoffDate && !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderByDescending(tsa => tsa.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSkillAssignment>> GetAssignmentsDueForAssessmentAsync(int daysOverdue = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOverdue);
        
        return await _context.TechnicianSkillAssignments
            .Where(tsa => (tsa.LastAssessmentDate == null || 
                          tsa.LastAssessmentDate < cutoffDate) && 
                         !tsa.IsDeleted)
            .Include(tsa => tsa.Skill)
            .Include(tsa => tsa.Technician)
            .OrderBy(tsa => tsa.LastAssessmentDate)
            .ToListAsync();
    }

    public async Task UpdateLastAssessmentDateAsync(Guid assignmentId, DateTime assessmentDate)
    {
        var assignment = await GetByIdAsync(assignmentId);
        if (assignment != null)
        {
            assignment.LastAssessmentDate = assessmentDate;
            await UpdateAsync(assignment);
        }
    }
    
    public async Task<IEnumerable<TechnicianSkillAssignment>> GetByTechnicalSkillIdAsync(Guid skillId)
    {
        return await GetBySkillIdAsync(skillId);
    }
    
    public async Task<IEnumerable<TechnicianSkillAssignment>> GetExpiredCertificationsAsync()
    {
        return await GetExpiredAssignmentsAsync();
    }
    
    public async Task<IEnumerable<TechnicianSkillAssignment>> GetCertificationsExpiringInDaysAsync(int days)
    {
        var targetDate = DateTime.UtcNow.AddDays(days);
        return await GetExpiringAssignmentsAsync(targetDate);
    }
    
    public async Task<bool> HasTechnicianSkillAsync(Guid technicianId, Guid skillId)
    {
        return await HasSkillAssignmentAsync(technicianId, skillId);
    }
    
    public async Task<IEnumerable<Employee>> GetTechniciansBySkillAsync(Guid skillId, int? minProficiencyLevel = null)
    {
        var assignments = await GetTechniciansWithSkillAsync(skillId, minProficiencyLevel);
        return assignments.Select(a => a.Technician).Where(t => t != null).Cast<Employee>();
    }
}
