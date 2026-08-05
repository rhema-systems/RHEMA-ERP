using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// COMPETENCY REPOSITORY
// ============================================================================

#region Competency Repository

public class CompetencyRepository : GenericRepository<Competency>, ICompetencyRepository
{
    public CompetencyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Competency?> GetByCodeAsync(string code)
    {
        var normalized = (code ?? string.Empty).Trim();
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Code.ToLower() == normalized.ToLower());
    }

    public async Task<IEnumerable<Competency>> GetByCategoryAsync(CompetencyCategory category)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.CompetencyCategory == category)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Competency>> GetActiveAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.CompetencyCategory)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Competency?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.SkillIndicators).ThenInclude(i => i.Skill)
            .Include(c => c.PositionCompetencies)
            .Include(c => c.EmployeeCompetencies)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<Competency>> GetByPositionAsync(Guid positionId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.PositionCompetencies.Any(pc => pc.PositionId == positionId && !pc.IsDeleted))
            .OrderBy(c => c.CompetencyCategory)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Competency>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.EmployeeCompetencies.Any(ec => ec.EmployeeId == employeeId && !ec.IsDeleted))
            .OrderBy(c => c.CompetencyCategory)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null)
    {
        var normalized = (code ?? string.Empty).Trim().ToLower();
        var query = _dbSet
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Code.ToLower() == normalized);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}

#endregion

// ============================================================================
// COMPETENCY SKILL INDICATOR REPOSITORY
// ============================================================================

#region CompetencySkillIndicator Repository

public class CompetencySkillIndicatorRepository : GenericRepository<CompetencySkillIndicator>, ICompetencySkillIndicatorRepository
{
    public CompetencySkillIndicatorRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CompetencySkillIndicator>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(i => i.Skill)
            .Where(i => i.CompetencyId == competencyId && !i.IsDeleted)
            .OrderBy(i => i.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompetencySkillIndicator>> GetBySkillIdAsync(Guid skillId)
    {
        return await _dbSet
            .Include(i => i.Competency)
            .Where(i => i.SkillId == skillId && !i.IsDeleted)
            .OrderBy(i => i.Competency.Name)
            .ToListAsync();
    }

    public async Task<CompetencySkillIndicator?> GetByCompetencyAndSkillAsync(Guid competencyId, Guid skillId)
    {
        return await _dbSet
            .Include(i => i.Competency)
            .Include(i => i.Skill)
            .FirstOrDefaultAsync(i => i.CompetencyId == competencyId
                                   && i.SkillId == skillId
                                   && !i.IsDeleted);
    }

    public async Task<IEnumerable<CompetencySkillIndicator>> GetByMinimumSkillLevelAsync(SkillLevel minimumLevel)
    {
        return await _dbSet
            .Include(i => i.Competency)
            .Include(i => i.Skill)
            .Where(i => !i.IsDeleted && i.MinimumSkillLevelRequired >= minimumLevel)
            .OrderBy(i => i.Competency.Name)
            .ThenBy(i => i.Skill.Name)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// POSITION COMPETENCY REPOSITORY
// ============================================================================

#region PositionCompetency Repository

public class PositionCompetencyRepository : GenericRepository<PositionCompetency>, IPositionCompetencyRepository
{
    public PositionCompetencyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PositionCompetency>> GetByPositionIdAsync(Guid positionId)
    {
        return await _dbSet
            .Include(pc => pc.Competency)
            .Where(pc => pc.PositionId == positionId && !pc.IsDeleted)
            .OrderBy(pc => pc.Competency.CompetencyCategory)
            .ThenBy(pc => pc.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<PositionCompetency>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(pc => pc.Position)
            .Where(pc => pc.CompetencyId == competencyId && !pc.IsDeleted)
            .OrderBy(pc => pc.Position.Title)
            .ToListAsync();
    }

    public async Task<PositionCompetency?> GetByPositionAndCompetencyAsync(Guid positionId, Guid competencyId)
    {
        return await _dbSet
            .Include(pc => pc.Competency)
            .Include(pc => pc.Position)
            .FirstOrDefaultAsync(pc => pc.PositionId == positionId
                                    && pc.CompetencyId == competencyId
                                    && !pc.IsDeleted);
    }

    public async Task<IEnumerable<PositionCompetency>> GetByMinimumRequiredLevelAsync(int minimumLevel)
    {
        return await _dbSet
            .Include(pc => pc.Competency)
            .Include(pc => pc.Position)
            .Where(pc => !pc.IsDeleted && pc.RequiredProficiencyLevel >= minimumLevel)
            .OrderByDescending(pc => pc.RequiredProficiencyLevel)
            .ThenBy(pc => pc.Position.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Replaces all existing competency requirements for a position in a single database
    /// round-trip: soft-deletes the current set, then inserts the new set.
    /// </summary>
    public async Task BulkReplaceForPositionAsync(Guid positionId, IEnumerable<PositionCompetency> newRequirements)
    {
        var existing = await _dbSet
            .Where(pc => pc.PositionId == positionId && !pc.IsDeleted)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var record in existing)
        {
            record.IsDeleted = true;
            record.DeletedAt = now;
        }

        await _dbSet.AddRangeAsync(newRequirements);
    }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY REPOSITORY
// ============================================================================

#region EmployeeCompetency Repository

public class EmployeeCompetencyRepository : GenericRepository<EmployeeCompetency>, IEmployeeCompetencyRepository
{
    public EmployeeCompetencyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeCompetency>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(ec => ec.Competency)
            .Include(ec => ec.AssessedBy)
            .Where(ec => ec.EmployeeId == employeeId && !ec.IsDeleted)
            .OrderBy(ec => ec.Competency.CompetencyCategory)
            .ThenBy(ec => ec.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(ec => ec.Employee)
            .Include(ec => ec.AssessedBy)
            .Where(ec => ec.CompetencyId == competencyId && !ec.IsDeleted)
            .OrderBy(ec => ec.Employee.LastName)
            .ThenBy(ec => ec.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<EmployeeCompetency?> GetByEmployeeAndCompetencyAsync(Guid employeeId, Guid competencyId)
    {
        return await _dbSet
            .Include(ec => ec.Competency)
            .Include(ec => ec.Employee)
            .Include(ec => ec.AssessedBy)
            .FirstOrDefaultAsync(ec => ec.EmployeeId == employeeId
                                    && ec.CompetencyId == competencyId
                                    && !ec.IsDeleted);
    }

    public async Task<EmployeeCompetency?> GetWithHistoryAsync(Guid id)
    {
        return await _dbSet
            .Include(ec => ec.Competency)
            .Include(ec => ec.Employee)
            .Include(ec => ec.AssessedBy)
            .Include(ec => ec.History).ThenInclude(h => h.AssessedBy)
            .Include(ec => ec.History).ThenInclude(h => h.RecordedBy)
            .FirstOrDefaultAsync(ec => ec.Id == id && !ec.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetByEmployeeAndMinimumLevelAsync(Guid employeeId, int minimumLevel)
    {
        return await _dbSet
            .Include(ec => ec.Competency)
            .Where(ec => ec.EmployeeId == employeeId
                      && !ec.IsDeleted
                      && ec.CurrentProficiencyLevel >= minimumLevel)
            .OrderByDescending(ec => ec.CurrentProficiencyLevel)
            .ThenBy(ec => ec.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetByPositionCompetenciesAsync(Guid positionId)
    {
        // Fetch the required competency IDs for the position first to avoid a complex join
        var requiredCompetencyIds = await _context.Set<PositionCompetency>()
            .AsNoTracking()
            .Where(pc => pc.PositionId == positionId && !pc.IsDeleted)
            .Select(pc => pc.CompetencyId)
            .ToListAsync();

        if (!requiredCompetencyIds.Any())
            return Enumerable.Empty<EmployeeCompetency>();

        return await _dbSet
            .Include(ec => ec.Employee)
            .Include(ec => ec.Competency)
            .Where(ec => !ec.IsDeleted && requiredCompetencyIds.Contains(ec.CompetencyId))
            .OrderBy(ec => ec.Employee.LastName)
            .ThenBy(ec => ec.Employee.FirstName)
            .ThenBy(ec => ec.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetQualifiedEmployeesForPositionAsync(Guid positionId)
    {
        // Load position requirements
        var requirements = await _context.Set<PositionCompetency>()
            .AsNoTracking()
            .Where(pc => pc.PositionId == positionId && !pc.IsDeleted)
            .Select(pc => new { pc.CompetencyId, pc.RequiredProficiencyLevel })
            .ToListAsync();

        if (!requirements.Any())
            return Enumerable.Empty<EmployeeCompetency>();

        var competencyIds = requirements.Select(r => r.CompetencyId).ToList();

        // Pull all assessments for these competencies
        var assessments = await _dbSet
            .Include(ec => ec.Employee)
            .Include(ec => ec.Competency)
            .Where(ec => !ec.IsDeleted && competencyIds.Contains(ec.CompetencyId))
            .ToListAsync();

        // Group by employee and keep only those who meet or exceed every requirement
        var qualifiedEmployeeIds = assessments
            .GroupBy(ec => ec.EmployeeId)
            .Where(g => requirements.All(req =>
                g.Any(ec => ec.CompetencyId == req.CompetencyId
                         && ec.CurrentProficiencyLevel >= req.RequiredProficiencyLevel)))
            .Select(g => g.Key)
            .ToHashSet();

        return assessments
            .Where(ec => qualifiedEmployeeIds.Contains(ec.EmployeeId))
            .ToList();
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetGapsForEmployeeAsync(Guid employeeId)
    {
        // Determine the employee's position
        var employee = await _context.Set<Employee>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted);

        if (employee == null)
            return Enumerable.Empty<EmployeeCompetency>();

        // Load required levels for the employee's position
        var requirements = await _context.Set<PositionCompetency>()
            .AsNoTracking()
            .Where(pc => pc.PositionId == employee.PositionId && !pc.IsDeleted)
            .Select(pc => new { pc.CompetencyId, pc.RequiredProficiencyLevel })
            .ToListAsync();

        if (!requirements.Any())
            return Enumerable.Empty<EmployeeCompetency>();

        var requiredIds = requirements.Select(r => r.CompetencyId).ToList();

        // Return assessed records that fall below the required level
        var assessed = await _dbSet
            .Include(ec => ec.Competency)
            .Where(ec => ec.EmployeeId == employeeId
                      && !ec.IsDeleted
                      && requiredIds.Contains(ec.CompetencyId))
            .ToListAsync();

        return assessed
            .Where(ec =>
            {
                var req = requirements.FirstOrDefault(r => r.CompetencyId == ec.CompetencyId);
                return req != null && ec.CurrentProficiencyLevel < req.RequiredProficiencyLevel;
            })
            .OrderBy(ec => ec.Competency.Name)
            .ToList();
    }

    public async Task<IEnumerable<EmployeeCompetency>> GetAssessmentsOlderThanAsync(DateTime olderThan)
    {
        return await _dbSet
            .Include(ec => ec.Employee)
            .Include(ec => ec.Competency)
            .Where(ec => !ec.IsDeleted && ec.AssessmentDate < olderThan)
            .OrderBy(ec => ec.AssessmentDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY HISTORY REPOSITORY
// ============================================================================

#region EmployeeCompetencyHistory Repository

public class EmployeeCompetencyHistoryRepository : GenericRepository<EmployeeCompetencyHistory>, IEmployeeCompetencyHistoryRepository
{
    public EmployeeCompetencyHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeCompetencyHistory>> GetByEmployeeCompetencyIdAsync(Guid employeeCompetencyId)
    {
        return await _dbSet
            .Include(h => h.AssessedBy)
            .Include(h => h.RecordedBy)
            .Where(h => h.EmployeeCompetencyId == employeeCompetencyId && !h.IsDeleted)
            .OrderByDescending(h => h.AssessmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCompetencyHistory>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(h => h.EmployeeCompetency).ThenInclude(ec => ec.Competency)
            .Include(h => h.AssessedBy)
            .Include(h => h.RecordedBy)
            .Where(h => !h.IsDeleted && h.EmployeeCompetency.EmployeeId == employeeId && !h.EmployeeCompetency.IsDeleted)
            .OrderByDescending(h => h.AssessmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCompetencyHistory>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(h => h.EmployeeCompetency).ThenInclude(ec => ec.Employee)
            .Include(h => h.AssessedBy)
            .Include(h => h.RecordedBy)
            .Where(h => !h.IsDeleted && h.EmployeeCompetency.CompetencyId == competencyId && !h.EmployeeCompetency.IsDeleted)
            .OrderByDescending(h => h.AssessmentDate)
            .ToListAsync();
    }

    public async Task<EmployeeCompetencyHistory?> GetLatestAsync(Guid employeeCompetencyId)
    {
        return await _dbSet
            .Include(h => h.AssessedBy)
            .Include(h => h.RecordedBy)
            .Where(h => h.EmployeeCompetencyId == employeeCompetencyId && !h.IsDeleted)
            .OrderByDescending(h => h.AssessmentDate)
            .FirstOrDefaultAsync();
    }
}

#endregion
