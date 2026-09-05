using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

#region Hazard

public class SheHazardRepository : GenericRepository<SheHazard>, ISheHazardRepository
{
    public SheHazardRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheHazard?> GetByCodeAsync(string code) =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .FirstOrDefaultAsync(h => h.Code == code && !h.IsDeleted);

    public async Task<SheHazard?> GetWithControlsAsync(Guid id) =>
        await _dbSet
            .Include(h => h.Location)
            .Include(h => h.Owner)
            .Include(h => h.LastReviewedBy)
            .Include(h => h.Controls).ThenInclude(c => c.ResponsiblePerson)
            .Include(h => h.CorrectiveActions).ThenInclude(a => a.CorrectiveActionTemplate)
            .AsSplitQuery()
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);

    public async Task<IEnumerable<SheHazard>> GetAllListAsync() =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetActiveAsync() =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.IsActive && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetByStatusAsync(SheHazardStatus status) =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.Status == status && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetByCategoryAsync(SheHazardCategory category) =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.Category == category && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetByResidualRiskLevelAsync(SheHazardRiskLevel level) =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.ResidualRiskLevel == level && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetByLocationAsync(Guid locationId) =>
        await _dbSet.Include(h => h.Owner)
            .Where(h => h.LocationId == locationId && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetByOwnerAsync(Guid ownerId) =>
        await _dbSet.Include(h => h.Location)
            .Where(h => h.OwnerId == ownerId && !h.IsDeleted)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetHighResidualRiskAsync(int minimumScore) =>
        await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.IsActive && !h.IsDeleted && h.ResidualRiskScore >= minimumScore)
            .OrderByDescending(h => h.ResidualRiskScore).ToListAsync();

    public async Task<IEnumerable<SheHazard>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(h => h.Location).Include(h => h.Owner)
            .Where(h => h.IsActive && !h.IsDeleted && h.ReviewDueDate != null && h.ReviewDueDate <= cutoff)
            .OrderBy(h => h.ReviewDueDate).ToListAsync();
    }
}

#endregion

#region Risk Assessment

public class SheRiskAssessmentRepository : GenericRepository<SheRiskAssessment>, ISheRiskAssessmentRepository
{
    public SheRiskAssessmentRepository(ApplicationDbContext context) : base(context) { }

    // AssessedHazards rides along because every summary row reports a HazardCount — without the
    // include (and no lazy loading anywhere in this app) the count reads 0 on every list. Split
    // query keeps the wide hazard-line text columns out of a single cartesian row.
    private IQueryable<SheRiskAssessment> WithListNavigations() =>
        _dbSet.Include(r => r.Location).Include(r => r.OrganizationUnit).Include(r => r.PreparedBy)
            .Include(r => r.AssessedHazards)
            .AsSplitQuery();

    public async Task<SheRiskAssessment?> GetByNumberAsync(string assessmentNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(r => r.AssessmentNumber == assessmentNumber && !r.IsDeleted);

    public async Task<SheRiskAssessment?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(r => r.Location)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.PreparedBy)
            .Include(r => r.ReviewedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.AssessedHazards).ThenInclude(h => h.Hazard)
            .Include(r => r.Acknowledgements).ThenInclude(a => a.Employee)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

    public async Task<IEnumerable<SheRiskAssessment>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.PreparedDate).ToListAsync();

    public async Task<IEnumerable<SheRiskAssessment>> GetByStatusAsync(SheRiskAssessmentStatus status) =>
        await WithListNavigations().Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.PreparedDate).ToListAsync();

    public async Task<IEnumerable<SheRiskAssessment>> GetByTypeAsync(SheRiskAssessmentType type) =>
        await WithListNavigations().Where(r => r.Type == type && !r.IsDeleted)
            .OrderByDescending(r => r.PreparedDate).ToListAsync();

    public async Task<IEnumerable<SheRiskAssessment>> GetByPreparerAsync(Guid preparedById) =>
        await WithListNavigations().Where(r => r.PreparedById == preparedById && !r.IsDeleted)
            .OrderByDescending(r => r.PreparedDate).ToListAsync();

    public async Task<IEnumerable<SheRiskAssessment>> GetExpiringAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(r => !r.IsDeleted && r.ValidUntil != null && r.ValidUntil <= cutoff
                     && (r.Status == SheRiskAssessmentStatus.Approved || r.Status == SheRiskAssessmentStatus.Active))
            .OrderBy(r => r.ValidUntil).ToListAsync();
    }

    public async Task<IEnumerable<SheRiskAssessment>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(r => !r.IsDeleted && r.NextReviewDate != null && r.NextReviewDate <= cutoff)
            .OrderBy(r => r.NextReviewDate).ToListAsync();
    }

    public async Task<IEnumerable<SheRiskAssessment>> GetActiveForAcknowledgementAsync() =>
        await _dbSet.Include(r => r.Location).Include(r => r.Acknowledgements)
            .Where(r => !r.IsDeleted
                     && (r.Status == SheRiskAssessmentStatus.Approved || r.Status == SheRiskAssessmentStatus.Active))
            .OrderByDescending(r => r.PreparedDate).ToListAsync();
}

#endregion

#region Inspection Checklist

public class SheInspectionChecklistRepository : GenericRepository<SheInspectionChecklist>, ISheInspectionChecklistRepository
{
    public SheInspectionChecklistRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheInspectionChecklist?> GetByNumberAsync(string checklistNumber) =>
        await _dbSet.FirstOrDefaultAsync(c => c.ChecklistNumber == checklistNumber && !c.IsDeleted);

    public async Task<SheInspectionChecklist?> GetWithItemsAsync(Guid id) =>
        await _dbSet
            .Include(c => c.Items.OrderBy(i => i.ItemOrder))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    public async Task<IEnumerable<SheInspectionChecklist>> GetActiveAsync() =>
        await _dbSet.Where(c => c.IsActive && !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();

    public async Task<IEnumerable<SheInspectionChecklist>> GetByTypeAsync(SheInspectionType type) =>
        await _dbSet.Where(c => c.Type == type && !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
}

#endregion

#region Safety Inspection

public class SafetyInspectionRepository : GenericRepository<SafetyInspection>, ISafetyInspectionRepository
{
    public SafetyInspectionRepository(ApplicationDbContext context) : base(context) { }

    // Items ride along because every summary row reports an OpenItemCount — without the include
    // (and no lazy loading anywhere in this app) the count reads 0 on every list. Split query
    // keeps the wide finding text columns out of a single cartesian row.
    private IQueryable<SafetyInspection> WithListNavigations() =>
        _dbSet.Include(i => i.Location).Include(i => i.OrganizationUnit).Include(i => i.Inspector)
            .Include(i => i.Items)
            .AsSplitQuery();

    public async Task<SafetyInspection?> GetByNumberAsync(string inspectionNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(i => i.InspectionNumber == inspectionNumber && !i.IsDeleted);

    public async Task<SafetyInspection?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(i => i.Location)
            .Include(i => i.OrganizationUnit)
            .Include(i => i.Checklist)
            .Include(i => i.Inspector)
            .Include(i => i.ClosedBy)
            .Include(i => i.Items).ThenInclude(t => t.ResponsiblePerson)
            .Include(i => i.Items).ThenInclude(t => t.ResolvedBy)
            .Include(i => i.Hazards).ThenInclude(h => h.Owner)
            .Include(i => i.Hazards).ThenInclude(h => h.Actions).ThenInclude(a => a.CorrectiveActionTemplate)
            .Include(i => i.Hazards).ThenInclude(h => h.Actions).ThenInclude(a => a.AssignedTo)
            .Include(i => i.Documents).ThenInclude(d => d.UploadedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

    public async Task<IEnumerable<SafetyInspection>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByStatusAsync(SheInspectionStatus status) =>
        await WithListNavigations().Where(i => i.Status == status && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByTypeAsync(SheInspectionType type) =>
        await WithListNavigations().Where(i => i.Type == type && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByCategoryAsync(SheInspectionCategory category) =>
        await WithListNavigations().Where(i => i.Category == category && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithListNavigations()
            .Where(i => i.InspectionDate >= fromDate && i.InspectionDate <= toDate && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByInspectorAsync(Guid inspectorId) =>
        await WithListNavigations().Where(i => i.InspectorId == inspectorId && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetByLocationAsync(Guid locationId) =>
        await WithListNavigations().Where(i => i.LocationId == locationId && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyInspection>> GetDueAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(i => !i.IsDeleted && i.NextInspectionDueDate != null && i.NextInspectionDueDate <= cutoff)
            .OrderBy(i => i.NextInspectionDueDate).ToListAsync();
    }

    public async Task<IEnumerable<SafetyInspection>> GetOpenWithFindingsAsync() =>
        await WithListNavigations()
            .Where(i => !i.IsDeleted
                     && i.Status != SheInspectionStatus.Closed
                     && i.Items.Any(t => !t.IsResolved))
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<string> GetNextInspectionNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INSP-{year}-";
        var last = await _dbSet.IgnoreQueryFilters()
            .Where(i => i.InspectionNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InspectionNumber)
            .Select(i => i.InspectionNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }
}

#endregion
