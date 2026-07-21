using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Reference Catalog (A) & Incident Management (B).
// ============================================================================

#region Reference Catalog

public class SheIncidentTypeRepository : GenericRepository<SheIncidentType>, ISheIncidentTypeRepository
{
    public SheIncidentTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheIncidentType?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<SheIncidentType>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();

    public async Task<IEnumerable<SheIncidentType>> GetByCategoryAsync(SheIncidentCategory category) =>
        await _dbSet.Where(t => t.Category == category && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();

    public async Task<IEnumerable<SheIncidentType>> GetReportableAsync() =>
        await _dbSet.Include(t => t.RegulatoryBody)
            .Where(t => t.IsReportable && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();

    public async Task<SheIncidentType?> GetWithDefaultActionsAsync(Guid id) =>
        await _dbSet
            .Include(t => t.RegulatoryBody)
            .Include(t => t.DefaultCorrectiveActions).ThenInclude(a => a.CorrectiveActionTemplate)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
}

public class SheInjuryTypeRepository : GenericRepository<SheInjuryType>, ISheInjuryTypeRepository
{
    public SheInjuryTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheInjuryType?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<SheInjuryType>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();
}

public class SheBodyPartRepository : GenericRepository<SheBodyPart>, ISheBodyPartRepository
{
    public SheBodyPartRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheBodyPart?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<SheBodyPart>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();
}

public class SheCorrectiveActionTemplateRepository : GenericRepository<SheCorrectiveActionTemplate>, ISheCorrectiveActionTemplateRepository
{
    public SheCorrectiveActionTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheCorrectiveActionTemplate?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<SheCorrectiveActionTemplate>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Title).ToListAsync();

    public async Task<IEnumerable<SheCorrectiveActionTemplate>> GetByCategoryAsync(SheCorrectiveActionCategory category) =>
        await _dbSet.Where(t => t.Category == category && !t.IsDeleted).OrderBy(t => t.Title).ToListAsync();
}

public class SheRegulatoryBodyRepository : GenericRepository<SheRegulatoryBody>, ISheRegulatoryBodyRepository
{
    public SheRegulatoryBodyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SheRegulatoryBody>> GetActiveAsync() =>
        await _dbSet.Where(b => b.IsActive && !b.IsDeleted).OrderBy(b => b.Name).ToListAsync();

    public async Task<IEnumerable<SheRegulatoryBody>> GetByDomainAsync(SheRegulatoryDomain domain) =>
        await _dbSet.Where(b => b.Domain == domain && !b.IsDeleted).OrderBy(b => b.Name).ToListAsync();
}

#endregion

#region Safety Incident

public class SafetyIncidentRepository : GenericRepository<SafetyIncident>, ISafetyIncidentRepository
{
    public SafetyIncidentRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SafetyIncident> WithListNavigations() =>
        _dbSet
            .Include(i => i.IncidentType)
            .Include(i => i.Location)
            .Include(i => i.OrganizationUnit)
            .Include(i => i.ReportedBy);

    public async Task<SafetyIncident?> GetByIncidentNumberAsync(string incidentNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(i => i.IncidentNumber == incidentNumber && !i.IsDeleted);

    public async Task<SafetyIncident?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(i => i.IncidentType)
            .Include(i => i.Location)
            .Include(i => i.OrganizationUnit)
            .Include(i => i.Supervisor)
            .Include(i => i.ReportedBy)
            .Include(i => i.LeadInvestigator)
            .Include(i => i.ReportedToBody)
            .Include(i => i.AuthorityNotifiedBy)
            .Include(i => i.InsuranceProvider)
            .Include(i => i.ReviewedBy)
            .Include(i => i.ClosedBy)
            .Include(i => i.InvolvedPersons).ThenInclude(p => p.Employee)
            .Include(i => i.InvolvedPersons).ThenInclude(p => p.InjuryType)
            .Include(i => i.InvolvedPersons).ThenInclude(p => p.InjuredBodyParts).ThenInclude(b => b.BodyPart)
            .Include(i => i.Witnesses).ThenInclude(w => w.Employee)
            .Include(i => i.InvestigationTeam).ThenInclude(m => m.Employee)
            .Include(i => i.CorrectiveActions).ThenInclude(c => c.ResponsiblePerson)
            .Include(i => i.FollowUps).ThenInclude(f => f.ConductedBy)
            .Include(i => i.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

    public async Task<IEnumerable<SafetyIncident>> GetByStatusAsync(SheIncidentStatus status) =>
        await WithListNavigations().Where(i => i.Status == status && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetBySeverityAsync(SheIncidentSeverity severity) =>
        await WithListNavigations().Where(i => i.Severity == severity && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetByCategoryAsync(SheIncidentCategory category) =>
        await WithListNavigations().Where(i => i.Category == category && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithListNavigations()
            .Where(i => i.IncidentDate >= fromDate && i.IncidentDate <= toDate && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetByLocationAsync(Guid locationId) =>
        await WithListNavigations().Where(i => i.LocationId == locationId && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetByOrganizationUnitAsync(Guid organizationUnitId) =>
        await WithListNavigations().Where(i => i.OrganizationUnitId == organizationUnitId && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetRequiringInvestigationAsync() =>
        await WithListNavigations()
            .Where(i => i.RequiresInvestigation && i.Status != SheIncidentStatus.Closed && !i.IsDeleted)
            .OrderByDescending(i => i.Severity).ThenByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetOpenAsync() =>
        await WithListNavigations()
            .Where(i => i.Status != SheIncidentStatus.Closed && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetLostTimeInjuriesAsync() =>
        await WithListNavigations().Where(i => i.IsLostTimeInjury && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetByInvolvedEmployeeAsync(Guid employeeId) =>
        await WithListNavigations()
            .Where(i => i.InvolvedPersons.Any(p => p.EmployeeId == employeeId) && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetForEmployeeAsync(Guid employeeId) =>
        await WithListNavigations()
            .Where(i => !i.IsDeleted && (i.ReportedById == employeeId
                        || i.InvolvedPersons.Any(p => p.EmployeeId == employeeId)))
            .OrderByDescending(i => i.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncident>> GetReportableNotYetNotifiedAsync() =>
        await WithListNavigations()
            .Where(i => i.ReportableToAuthority && i.AuthorityNotificationDate == null && !i.IsDeleted)
            .OrderBy(i => i.IncidentDate).ToListAsync();

    public async Task<(IEnumerable<SafetyIncident> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, SheIncidentStatus? status = null)
    {
        var query = WithListNavigations().Where(i => !i.IsDeleted);
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.IncidentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<string> GetNextIncidentNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INC-{year}-";
        var last = await _dbSet.IgnoreQueryFilters()
            .Where(i => i.IncidentNumber.StartsWith(prefix))
            .OrderByDescending(i => i.IncidentNumber)
            .Select(i => i.IncidentNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }
}

public class SafetyIncidentCorrectiveActionRepository : GenericRepository<SafetyIncidentCorrectiveAction>, ISafetyIncidentCorrectiveActionRepository
{
    public SafetyIncidentCorrectiveActionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByIncidentIdAsync(Guid incidentId) =>
        await _dbSet.Include(c => c.ResponsiblePerson)
            .Where(c => c.IncidentId == incidentId && !c.IsDeleted)
            .OrderBy(c => c.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByResponsiblePersonAsync(Guid employeeId) =>
        await _dbSet.Include(c => c.Incident)
            .Where(c => c.ResponsiblePersonId == employeeId && !c.IsDeleted)
            .OrderBy(c => c.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByStatusAsync(SheCorrectiveActionStatus status) =>
        await _dbSet.Include(c => c.ResponsiblePerson)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderBy(c => c.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet.Include(c => c.ResponsiblePerson).Include(c => c.Incident)
            .Where(c => !c.IsDeleted
                     && c.DueDate < today
                     && c.Status != SheCorrectiveActionStatus.Completed
                     && c.Status != SheCorrectiveActionStatus.Verified
                     && c.Status != SheCorrectiveActionStatus.Cancelled)
            .OrderBy(c => c.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetPendingVerificationAsync() =>
        await _dbSet.Include(c => c.ResponsiblePerson).Include(c => c.Incident)
            .Where(c => c.Status == SheCorrectiveActionStatus.Completed && !c.EffectivenessVerified && !c.IsDeleted)
            .OrderBy(c => c.CompletionDate).ToListAsync();
}

#endregion
