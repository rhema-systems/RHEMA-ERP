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

    /// <remarks>
    /// <c>AsSplitQuery</c> is required, not an optimisation. This is 32 includes spanning eight
    /// collections; as a single query SQL Server builds one row per combination of children and the
    /// plan exceeds the 8060-byte worktable row limit, so the read fails outright the moment a case
    /// has more than a trivial number of children. The same shape was fixed on the SHE incident,
    /// inspection and permit detail reads and on staff-movement create.
    /// </remarks>
    public async Task<StaffDisciplinaryAction?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
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
            .Include(d => d.LegalReviews).ThenInclude(lr => lr.ExternalCounsel)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
    }

    public async Task<StaffDisciplinaryAction?> GetByCaseNumberAsync(string caseNumber)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.ReportedBy)
            .Include(d => d.ActionType)
            .FirstOrDefaultAsync(d => d.CaseNumber == caseNumber && !d.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Warning)
            .Include(d => d.Suspension)
            .Include(d => d.Fine)
            .Include(d => d.Termination)
            .Include(d => d.Appeal)
            .Where(d => d.EmployeeId == employeeId && !d.IsDeleted)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByStatusAsync(DisciplinaryStatus status)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => d.Status == status && !d.IsDeleted)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByOffenseAsync(Guid offenseId)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => d.StaffOffenseId == offenseId && !d.IsDeleted)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetBySeverityAsync(StaffOffenseSeverity minimumSeverity)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => d.Severity >= minimumSeverity && !d.IsDeleted)
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetOpenCasesAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => !d.IsDeleted
                     && d.Status != DisciplinaryStatus.Closed
                     && d.Status != DisciplinaryStatus.Dismissed)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingInvestigationAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Investigation)
            .Where(d => !d.IsDeleted && d.RequiresInvestigation && d.Investigation == null)
            .OrderBy(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingHearingAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Hearing)
            .Where(d => !d.IsDeleted && d.HearingRequired && d.Hearing == null)
            .OrderBy(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetPendingClosureAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.ActionType)
            .Where(d => !d.IsDeleted && d.Status == DisciplinaryStatus.DecisionMade)
            .OrderBy(d => d.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveWarningAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Warning)
            .Where(d => !d.IsDeleted
                     && d.Warning != null
                     && (d.Warning.WarningExpiryDate == null || d.Warning.WarningExpiryDate > today))
            .OrderBy(d => d.Warning!.WarningExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveSuspensionAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Suspension)
            .Where(d => !d.IsDeleted
                     && d.Suspension != null
                     && (d.Suspension.SuspensionStartDate == null || d.Suspension.SuspensionStartDate <= today)
                     && (d.Suspension.SuspensionEndDate == null || d.Suspension.SuspensionEndDate >= today))
            .OrderBy(d => d.Suspension!.SuspensionEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithOutstandingFineAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Fine)
            .Where(d => !d.IsDeleted
                     && d.Fine != null
                     && d.Fine.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .OrderBy(d => d.Fine!.FineDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithPendingTerminationAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Termination)
            .Include(d => d.Separation)
            .Where(d => !d.IsDeleted && d.Termination != null && d.Separation == null)
            .OrderByDescending(d => d.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveAppealAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.Appeal)
            .Where(d => !d.IsDeleted
                     && d.Appeal != null
                     && d.Appeal.AppealStatus != DisciplineAppealStatus.DecisionMade
                     && d.Appeal.AppealStatus != DisciplineAppealStatus.Dismissed)
            .OrderByDescending(d => d.Appeal!.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveLegalReviewAsync()
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Include(d => d.LegalReviews)
            .Where(d => !d.IsDeleted
                     && d.LegalReviews.Any(lr => !lr.IsDeleted && lr.LegalReviewCompleteDate == null))
            .OrderByDescending(d => d.LegalReviews
                .Where(lr => !lr.IsDeleted && lr.LegalReviewCompleteDate == null)
                .Min(lr => lr.ReferredToLegalDate))
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByIncidentDateRangeAsync(DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => !d.IsDeleted && d.IncidentDate >= from && d.IncidentDate <= to)
            .OrderByDescending(d => d.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplinaryAction>> GetByReportedDateRangeAsync(DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.StaffOffense)
            .Where(d => !d.IsDeleted && d.ReportedDate >= from && d.ReportedDate <= to)
            .OrderByDescending(d => d.ReportedDate)
            .ToListAsync();
    }

    public async Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId)
    {
        return await _dbSet
            .AnyAsync(d => d.CaseNumber == caseNumber && d.TenantId == tenantId && !d.IsDeleted);
    }

    public async Task<int> GetOpenCaseCountForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .CountAsync(d => d.EmployeeId == employeeId
                          && !d.IsDeleted
                          && d.Status != DisciplinaryStatus.Closed
                          && d.Status != DisciplinaryStatus.Dismissed);
    }
}

#endregion
