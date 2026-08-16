using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINE SUB-ENTITY REPOSITORIES
//
// Every repository in this file follows one shape: a private Scoped(tenantId) helper that owns the
// tenant predicate, the soft-delete predicate and the include set, and reads that add nothing but
// their own filter and ordering.
//
// This replaced two defects at once. Every read used to fetch EVERY TENANT'S rows and leave the
// service to filter them in memory, which is a cross-tenant read however the caller is gated. And
// each read carried a slightly different include set, so the same entity came back with different
// fields populated depending on which endpoint you asked — the detail reads were consistently
// NARROWER than the list reads, which is why a case's offense name rendered blank on the very screen
// that exists to show it.
// ============================================================================

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION REPOSITORY
// ============================================================================

#region Staff Discipline Investigation Repository

public class StaffDisciplineInvestigationRepository
    : GenericRepository<StaffDisciplineInvestigation>, IStaffDisciplineInvestigationRepository
{
    public StaffDisciplineInvestigationRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineInvestigation> Scoped(Guid tenantId) =>
        _dbSet
            .Where(i => i.TenantId == tenantId && !i.IsDeleted)
            .Include(i => i.Investigator)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineInvestigation?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(i => i.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetByInvestigatorAsync(Guid tenantId, Guid investigatorId)
    {
        return await Scoped(tenantId)
            .Where(i => i.InvestigatorId == investigatorId && i.InvestigationEndDate == null)
            .OrderBy(i => i.InvestigationStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetOpenInvestigationsAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(i => i.InvestigationEndDate == null)
            .OrderBy(i => i.InvestigationStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetOverdueInvestigationsAsync(Guid tenantId, int maxDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxDays);
        return await Scoped(tenantId)
            .Where(i => i.InvestigationEndDate == null
                     && i.InvestigationStartDate != null
                     && i.InvestigationStartDate <= cutoff)
            .OrderBy(i => i.InvestigationStartDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING REPOSITORY
// ============================================================================

#region Staff Discipline Hearing Repository

public class StaffDisciplineHearingRepository
    : GenericRepository<StaffDisciplineHearing>, IStaffDisciplineHearingRepository
{
    public StaffDisciplineHearingRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineHearing> Scoped(Guid tenantId) =>
        _dbSet
            .Where(h => h.TenantId == tenantId && !h.IsDeleted)
            .Include(h => h.HearingOfficer)
            .Include(h => h.RepresentativeEmployee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineHearing?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(h => h.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetByHearingOfficerAsync(Guid tenantId, Guid officerId)
    {
        return await Scoped(tenantId)
            .Where(h => h.HearingOfficerId == officerId)
            .OrderBy(h => h.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetUpcomingHearingsAsync(Guid tenantId, int daysAhead = 14)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await Scoped(tenantId)
            .Where(h => h.HearingDate != null
                     && h.HearingDate >= now
                     && h.HearingDate <= cutoff)
            .OrderBy(h => h.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetAwaitingOutcomeAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(h => h.HearingDate != null
                     && h.HearingDate < today
                     && string.IsNullOrEmpty(h.HearingNotes))
            .OrderBy(h => h.HearingDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING REPOSITORY
// ============================================================================

#region Staff Discipline Warning Repository

public class StaffDisciplineWarningRepository
    : GenericRepository<StaffDisciplineWarning>, IStaffDisciplineWarningRepository
{
    public StaffDisciplineWarningRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineWarning> Scoped(Guid tenantId) =>
        _dbSet
            .Where(w => w.TenantId == tenantId && !w.IsDeleted)
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineWarning?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(w => w.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(w => w.DisciplinaryAction.EmployeeId == employeeId)
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetActiveWarningsForEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(w => w.DisciplinaryAction.EmployeeId == employeeId
                     && (w.WarningExpiryDate == null || w.WarningExpiryDate > today))
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetByTypeAsync(Guid tenantId, DisciplinaryWarningType warningType)
    {
        return await Scoped(tenantId)
            .Where(w => w.WarningType == warningType)
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetExpiringAsync(Guid tenantId, int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await Scoped(tenantId)
            .Where(w => w.WarningExpiryDate != null
                     && w.WarningExpiryDate >= now
                     && w.WarningExpiryDate <= cutoff)
            .OrderBy(w => w.WarningExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION REPOSITORY
// ============================================================================

#region Staff Discipline Suspension Repository

public class StaffDisciplineSuspensionRepository
    : GenericRepository<StaffDisciplineSuspension>, IStaffDisciplineSuspensionRepository
{
    public StaffDisciplineSuspensionRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineSuspension> Scoped(Guid tenantId) =>
        _dbSet
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineSuspension?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(s => s.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(s => s.DisciplinaryAction.EmployeeId == employeeId)
            .OrderByDescending(s => s.SuspensionStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetCurrentlyActiveAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(s => (s.SuspensionStartDate == null || s.SuspensionStartDate <= today)
                     && (s.SuspensionEndDate == null || s.SuspensionEndDate >= today))
            .OrderBy(s => s.SuspensionEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetUpcomingAsync(Guid tenantId, int daysAhead = 7)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await Scoped(tenantId)
            .Where(s => s.SuspensionStartDate != null
                     && s.SuspensionStartDate >= now
                     && s.SuspensionStartDate <= cutoff)
            .OrderBy(s => s.SuspensionStartDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE REPOSITORY
// ============================================================================

#region Staff Discipline Fine Repository

public class StaffDisciplineFineRepository
    : GenericRepository<StaffDisciplineFine>, IStaffDisciplineFineRepository
{
    public StaffDisciplineFineRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineFine> Scoped(Guid tenantId) =>
        _dbSet
            .Where(f => f.TenantId == tenantId && !f.IsDeleted)
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineFine?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(f => f.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(f => f.DisciplinaryAction.EmployeeId == employeeId)
            .OrderByDescending(f => f.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetOutstandingAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(f => f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .OrderBy(f => f.FineDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetOverdueAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(f => f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid
                     && f.FineDueDate != null
                     && f.FineDueDate < today)
            .OrderBy(f => f.FineDueDate)
            .ToListAsync();
    }

    /// <remarks>
    /// A money figure, so the tenant predicate matters more here than anywhere else in the file:
    /// unscoped, this summed another tenant's fines into this tenant's outstanding balance.
    /// </remarks>
    public async Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await _dbSet
            .Where(f => f.TenantId == tenantId
                     && !f.IsDeleted
                     && f.DisciplinaryAction.EmployeeId == employeeId
                     && f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .SumAsync(f => (f.FineAmount ?? 0m) - (f.FinePaidAmount ?? 0m));
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION REPOSITORY
// ============================================================================

#region Staff Discipline Termination Repository

public class StaffDisciplineTerminationRepository
    : GenericRepository<StaffDisciplineTermination>, IStaffDisciplineTerminationRepository
{
    public StaffDisciplineTerminationRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineTermination> Scoped(Guid tenantId) =>
        _dbSet
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineTermination?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(t => t.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetByTypeAsync(Guid tenantId, EmployeeTerminationType type)
    {
        return await Scoped(tenantId)
            .Where(t => t.Type == type)
            .OrderByDescending(t => t.DisciplinaryAction.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetEligibleForRehireAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(t => t.IsEligibleForRehire)
            .OrderBy(t => t.EligibleForRehireDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetPendingPaycheckProcessingAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(t => !t.FinalPaycheckProcessed)
            .OrderByDescending(t => t.DisciplinaryAction.DecisionDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SEPARATION REPOSITORY
// ============================================================================

#region Staff Discipline Separation Repository

public class StaffDisciplineSeparationRepository
    : GenericRepository<StaffDisciplineSeparation>, IStaffDisciplineSeparationRepository
{
    public StaffDisciplineSeparationRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineSeparation> Scoped(Guid tenantId) =>
        _dbSet
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Include(s => s.ExitInterviewer)
            .Include(s => s.AccessRevokedBy)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.StaffOffense);

    public async Task<StaffDisciplineSeparation?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(s => s.DisciplinaryActionId == caseId);
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetIncompleteAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(s => !s.ExitChecklistCompleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetByInterviewerAsync(Guid tenantId, Guid interviewerId)
    {
        return await Scoped(tenantId)
            .Where(s => s.ExitInterviewerId == interviewerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetPendingAccessRevocationAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(s => !s.AccessRevoked)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetPendingEquipmentReturnAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(s => !s.EquipmentReturned)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL REPOSITORY
// ============================================================================

#region Staff Discipline Appeal Repository

public class StaffDisciplineAppealRepository
    : GenericRepository<StaffDisciplineAppeal>, IStaffDisciplineAppealRepository
{
    public StaffDisciplineAppealRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineAppeal> Scoped(Guid tenantId) =>
        _dbSet
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.AppealOutcomeBy)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<StaffDisciplineAppeal?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(a => a.DisciplinaryActionId == caseId);
    }

    public async Task<StaffDisciplineAppeal?> GetWithFullDetailsAsync(Guid tenantId, Guid id)
    {
        return await Scoped(tenantId)
            .Include(a => a.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByStatusAsync(Guid tenantId, DisciplineAppealStatus status)
    {
        return await Scoped(tenantId)
            .Where(a => a.AppealStatus == status)
            .OrderByDescending(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetPendingHearingScheduleAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(a => (a.AppealStatus == DisciplineAppealStatus.Filed
                      || a.AppealStatus == DisciplineAppealStatus.UnderReview)
                     && a.HearingDate == null)
            .OrderBy(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetAwaitingOutcomeAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(a => a.HearingDate != null
                     && a.HearingDate < today
                     && a.AppealOutcome == null)
            .OrderBy(a => a.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByAppealOfficerAsync(Guid tenantId, Guid officerId)
    {
        return await Scoped(tenantId)
            .Where(a => a.AppealOfficerId == officerId)
            .OrderByDescending(a => a.FiledDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION REPOSITORY
// ============================================================================

#region Staff Discipline Corrective Action Repository

public class StaffDisciplineCorrectiveActionRepository
    : GenericRepository<StaffDisciplineCorrectiveAction>, IStaffDisciplineCorrectiveActionRepository
{
    public StaffDisciplineCorrectiveActionRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineCorrectiveAction> Scoped(Guid tenantId) =>
        _dbSet
            .Where(ca => ca.TenantId == tenantId && !ca.IsDeleted)
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.Items)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<StaffDisciplineCorrectiveAction?> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(ca => ca.DisciplinaryActionId == caseId);
    }

    public async Task<StaffDisciplineCorrectiveAction?> GetWithItemsAsync(Guid tenantId, Guid id)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(ca => ca.Id == id);
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(ca => ca.EmployeeId == employeeId)
            .OrderByDescending(ca => ca.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetBySupervisorAsync(Guid tenantId, Guid supervisorId)
    {
        return await Scoped(tenantId)
            .Where(ca => ca.SupervisorId == supervisorId)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByStatusAsync(Guid tenantId, DisciplineCorrectiveActionStatus status)
    {
        return await Scoped(tenantId)
            .Where(ca => ca.Status == status)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetOverdueAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(ca => ca.ReviewDate < today
                      && ca.Status != DisciplineCorrectiveActionStatus.Completed
                      && ca.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetDueForReviewAsync(Guid tenantId, int daysAhead = 14)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await Scoped(tenantId)
            .Where(ca => ca.ReviewDate >= now
                      && ca.ReviewDate <= cutoff
                      && ca.Status != DisciplineCorrectiveActionStatus.Completed
                      && ca.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION ITEM REPOSITORY
// ============================================================================

#region Staff Discipline Corrective Action Item Repository

public class StaffDisciplineCorrectiveActionItemRepository
    : GenericRepository<StaffDisciplineCorrectiveActionItem>, IStaffDisciplineCorrectiveActionItemRepository
{
    public StaffDisciplineCorrectiveActionItemRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineCorrectiveActionItem> Scoped(Guid tenantId) =>
        _dbSet
            .Where(i => i.TenantId == tenantId && !i.IsDeleted)
            .Include(i => i.CorrectiveAction).ThenInclude(ca => ca.Employee)
            .Include(i => i.CorrectiveAction).ThenInclude(ca => ca.DisciplinaryAction);

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetByCorrectiveActionIdAsync(Guid tenantId, Guid correctiveActionId)
    {
        return await Scoped(tenantId)
            .Where(i => i.CorrectiveActionId == correctiveActionId)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetPendingByCorrectiveActionAsync(Guid tenantId, Guid correctiveActionId)
    {
        return await Scoped(tenantId)
            .Where(i => i.CorrectiveActionId == correctiveActionId
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueItemsAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(i => i.TargetDate < today
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueByCorrectiveActionAsync(Guid tenantId, Guid correctiveActionId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(i => i.CorrectiveActionId == correctiveActionId
                     && i.TargetDate < today
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }
}

#endregion
