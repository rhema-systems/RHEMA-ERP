using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINARY ACTION REPOSITORY
// ============================================================================

#region Staff Disciplinary Action Repository

public class StaffDisciplinaryActionRepository
    : GenericRepository<StaffDisciplinaryAction>, IStaffDisciplinaryActionRepository
{
    public StaffDisciplinaryActionRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The one query every list read starts from: tenant-scoped, soft-delete-filtered, and carrying
    /// exactly the navigations <c>ToSummaryDto</c> touches.
    /// </summary>
    /// <remarks>
    /// The include set is not a convenience. <c>StaffDisciplinaryActionSummaryDto</c> derives
    /// HasWarning, HasSuspension, HasFine, HasTermination and AppealFiled from these navigations, and
    /// before this helper only <c>GetByEmployeeAsync</c> loaded them — so on twelve of the fourteen
    /// list endpoints all five flags came back false regardless of the truth, and the register's
    /// sanction badges quietly lied about which cases carried a penalty.
    /// </remarks>
    private IQueryable<StaffDisciplinaryAction> SummaryScoped(Guid tenantId) =>
        _dbSet
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.ActionType)
            .Include(d => d.Warning)
            .Include(d => d.Suspension)
            .Include(d => d.Fine)
            .Include(d => d.Termination)
            .Include(d => d.Appeal);

    /// <remarks>
    /// <c>AsSplitQuery</c> is required, not an optimisation. This is 32 includes spanning eight
    /// collections; as a single query SQL Server builds one row per combination of children and the
    /// plan exceeds the 8060-byte worktable row limit, so the read fails outright the moment a case
    /// has more than a trivial number of children. The same shape was fixed on the SHE incident,
    /// inspection and permit detail reads and on staff-movement create.
    /// </remarks>
    private IQueryable<StaffDisciplinaryAction> DetailScoped(Guid tenantId) =>
        _dbSet
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .AsSplitQuery()
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.ReportedBy)
            .Include(d => d.ReportedTo)
            .Include(d => d.DecisionBy)
            .Include(d => d.ClosedBy)
            .Include(d => d.ActionType)
            .Include(d => d.Investigation).ThenInclude(i => i!.Investigator)
            .Include(d => d.Hearing).ThenInclude(h => h!.HearingOfficer)
            .Include(d => d.Hearing).ThenInclude(h => h!.RepresentativeEmployee)
            .Include(d => d.Warning)
            .Include(d => d.Suspension)
            .Include(d => d.Fine)
            .Include(d => d.Termination)
            .Include(d => d.Separation).ThenInclude(s => s!.ExitInterviewer)
            .Include(d => d.Separation).ThenInclude(s => s!.AccessRevokedBy)
            .Include(d => d.Appeal).ThenInclude(a => a!.AppealOfficer)
            .Include(d => d.Appeal).ThenInclude(a => a!.AppealOutcomeBy)
            .Include(d => d.Appeal).ThenInclude(a => a!.Documents).ThenInclude(doc => doc.UploadedBy)
            .Include(d => d.CorrectiveAction).ThenInclude(ca => ca!.Employee)
            .Include(d => d.CorrectiveAction).ThenInclude(ca => ca!.Supervisor)
            .Include(d => d.CorrectiveAction).ThenInclude(ca => ca!.Items)
            .Include(d => d.ActionSteps).ThenInclude(s => s.OffenseProcedure)
            .Include(d => d.ActionSteps).ThenInclude(s => s.ActionedBy)
            .Include(d => d.ActionSteps).ThenInclude(s => s.Documents).ThenInclude(doc => doc.UploadedBy)
            .Include(d => d.Witnesses).ThenInclude(w => w.Employee)
            .Include(d => d.Documents).ThenInclude(doc => doc.UploadedBy)
            .Include(d => d.Notes).ThenInclude(n => n.CreatedByEmployee)
            .Include(d => d.Notifications).ThenInclude(n => n.SentBy)
            .Include(d => d.LegalReviews).ThenInclude(lr => lr.ReferredBy)
            .Include(d => d.LegalReviews).ThenInclude(lr => lr.ExternalCounsel);

    public async Task<StaffDisciplinaryAction?> GetWithFullDetailsAsync(Guid tenantId, Guid id)
    {
        return await DetailScoped(tenantId).FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<StaffDisciplinaryAction?> GetByCaseNumberAsync(Guid tenantId, string caseNumber)
    {
        return await SummaryScoped(tenantId)
            .Include(d => d.ReportedBy)
            .FirstOrDefaultAsync(d => d.CaseNumber == caseNumber);
    }

    /// <summary>
    /// The register. Paged in the database, not in memory, and over the same include set as every
    /// other list read — the service used to build this from a bare queryable with no includes at
    /// all, so the main case list rendered blank employee and offense names.
    /// </summary>
    public async Task<(IEnumerable<StaffDisciplinaryAction> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId, int pageNumber, int pageSize)
    {
        var query = SummaryScoped(tenantId);
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.IncidentDate)
            .ThenByDescending(d => d.ReportedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByStatusAsync(Guid tenantId, DisciplinaryStatus status)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByOffenseAsync(Guid tenantId, Guid offenseId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.StaffOffenseId == offenseId)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetBySeverityAsync(Guid tenantId, StaffOffenseSeverity minimumSeverity)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Severity >= minimumSeverity)
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetOpenCasesAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Status != DisciplinaryStatus.Closed
                     && d.Status != DisciplinaryStatus.Dismissed)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingInvestigationAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Include(d => d.Investigation)
            .Where(d => d.RequiresInvestigation && d.Investigation == null)
            .OrderBy(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingHearingAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Include(d => d.Hearing)
            .Where(d => d.HearingRequired && d.Hearing == null)
            .OrderBy(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingClosureAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Status == DisciplinaryStatus.DecisionMade)
            .OrderBy(d => d.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveWarningAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await SummaryScoped(tenantId)
            .Where(d => d.Warning != null
                     && (d.Warning.WarningExpiryDate == null || d.Warning.WarningExpiryDate > today))
            .OrderBy(d => d.Warning!.WarningExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveSuspensionAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await SummaryScoped(tenantId)
            .Where(d => d.Suspension != null
                     && (d.Suspension.SuspensionStartDate == null || d.Suspension.SuspensionStartDate <= today)
                     && (d.Suspension.SuspensionEndDate == null || d.Suspension.SuspensionEndDate >= today))
            .OrderBy(d => d.Suspension!.SuspensionEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithOutstandingFineAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Fine != null
                     && d.Fine.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .OrderBy(d => d.Fine!.FineDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithPendingTerminationAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Include(d => d.Separation)
            .Where(d => d.Termination != null && d.Separation == null)
            .OrderByDescending(d => d.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveAppealAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.Appeal != null
                     && d.Appeal.AppealStatus != DisciplineAppealStatus.DecisionMade
                     && d.Appeal.AppealStatus != DisciplineAppealStatus.Dismissed)
            .OrderByDescending(d => d.Appeal!.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveLegalReviewAsync(Guid tenantId)
    {
        return await SummaryScoped(tenantId)
            .Include(d => d.LegalReviews)
            .Where(d => d.LegalReviews.Any(lr => !lr.IsDeleted && lr.LegalReviewCompleteDate == null))
            .OrderByDescending(d => d.LegalReviews
                .Where(lr => !lr.IsDeleted && lr.LegalReviewCompleteDate == null)
                .Min(lr => lr.ReferredToLegalDate))
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByIncidentDateRangeAsync(Guid tenantId, DateTime from, DateTime to)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.IncidentDate >= from && d.IncidentDate <= to)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByReportedDateRangeAsync(Guid tenantId, DateTime from, DateTime to)
    {
        return await SummaryScoped(tenantId)
            .Where(d => d.ReportedDate >= from && d.ReportedDate <= to)
            .OrderByDescending(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId)
    {
        return await _dbSet
            .AnyAsync(d => d.CaseNumber == caseNumber && d.TenantId == tenantId && !d.IsDeleted);
    }

    public async Task<int> GetOpenCaseCountForEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await _dbSet
            .CountAsync(d => d.TenantId == tenantId
                          && d.EmployeeId == employeeId
                          && !d.IsDeleted
                          && d.Status != DisciplinaryStatus.Closed
                          && d.Status != DisciplinaryStatus.Dismissed);
    }
}

#endregion
