using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION REPOSITORY
// ============================================================================

#region Staff Discipline Investigation Repository

public class StaffDisciplineInvestigationRepository
    : GenericRepository<StaffDisciplineInvestigation>, IStaffDisciplineInvestigationRepository
{
    public StaffDisciplineInvestigationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffDisciplineInvestigation?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(i => i.Investigator)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(i => i.DisciplinaryActionId == caseId && !i.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetByInvestigatorAsync(Guid investigatorId)
    {
        return await _dbSet
            .Include(i => i.Investigator)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(i => i.InvestigatorId == investigatorId
                     && i.InvestigationEndDate == null
                     && !i.IsDeleted)
            .OrderBy(i => i.InvestigationStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetOpenInvestigationsAsync()
    {
        return await _dbSet
            .Include(i => i.Investigator)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(i => i.InvestigationEndDate == null && !i.IsDeleted)
            .OrderBy(i => i.InvestigationStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigation>> GetOverdueInvestigationsAsync(int maxDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxDays);
        return await _dbSet
            .Include(i => i.Investigator)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(i => i.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(i => !i.IsDeleted
                     && i.InvestigationEndDate == null
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

    public async Task<StaffDisciplineHearing?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(h => h.HearingOfficer)
            .Include(h => h.RepresentativeEmployee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(h => h.DisciplinaryActionId == caseId && !h.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetByHearingOfficerAsync(Guid officerId)
    {
        return await _dbSet
            .Include(h => h.HearingOfficer)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(h => h.HearingOfficerId == officerId && !h.IsDeleted)
            .OrderBy(h => h.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetUpcomingHearingsAsync(int daysAhead = 14)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(h => h.HearingOfficer)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(h => !h.IsDeleted
                     && h.HearingDate != null
                     && h.HearingDate >= DateTime.UtcNow
                     && h.HearingDate <= cutoff)
            .OrderBy(h => h.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineHearing>> GetAwaitingOutcomeAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(h => h.HearingOfficer)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(h => h.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(h => !h.IsDeleted
                     && h.HearingDate != null
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

    public async Task<StaffDisciplineWarning?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(w => w.DisciplinaryActionId == caseId && !w.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(w => w.DisciplinaryAction.EmployeeId == employeeId && !w.IsDeleted)
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetActiveWarningsForEmployeeAsync(Guid employeeId)
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(w => w.DisciplinaryAction.EmployeeId == employeeId
                     && !w.IsDeleted
                     && (w.WarningExpiryDate == null || w.WarningExpiryDate > today))
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetByTypeAsync(DisciplinaryWarningType warningType)
    {
        return await _dbSet
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(w => w.WarningType == warningType && !w.IsDeleted)
            .OrderByDescending(w => w.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWarning>> GetExpiringAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(w => w.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(w => !w.IsDeleted
                     && w.WarningExpiryDate != null
                     && w.WarningExpiryDate >= DateTime.UtcNow
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

    public async Task<StaffDisciplineSuspension?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(s => s.DisciplinaryActionId == caseId && !s.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(s => s.DisciplinaryAction.EmployeeId == employeeId && !s.IsDeleted)
            .OrderByDescending(s => s.SuspensionStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetCurrentlyActiveAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(s => !s.IsDeleted
                     && (s.SuspensionStartDate == null || s.SuspensionStartDate <= today)
                     && (s.SuspensionEndDate == null || s.SuspensionEndDate >= today))
            .OrderBy(s => s.SuspensionEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSuspension>> GetUpcomingAsync(int daysAhead = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(s => !s.IsDeleted
                     && s.SuspensionStartDate != null
                     && s.SuspensionStartDate >= DateTime.UtcNow
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

    public async Task<StaffDisciplineFine?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(f => f.DisciplinaryActionId == caseId && !f.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(f => f.DisciplinaryAction.EmployeeId == employeeId && !f.IsDeleted)
            .OrderByDescending(f => f.DisciplinaryAction.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetOutstandingAsync()
    {
        return await _dbSet
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(f => !f.IsDeleted
                     && f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .OrderBy(f => f.FineDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineFine>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(f => f.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(f => !f.IsDeleted
                     && f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid
                     && f.FineDueDate != null
                     && f.FineDueDate < today)
            .OrderBy(f => f.FineDueDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Where(f => f.DisciplinaryAction.EmployeeId == employeeId
                     && f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid
                     && !f.IsDeleted)
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

    public async Task<StaffDisciplineTermination?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(t => t.DisciplinaryActionId == caseId && !t.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetByTypeAsync(EmployeeTerminationType type)
    {
        return await _dbSet
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.StaffOffense)
            .Where(t => t.Type == type && !t.IsDeleted)
            .OrderByDescending(t => t.DisciplinaryAction.DecisionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetEligibleForRehireAsync()
    {
        return await _dbSet
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(t => t.IsEligibleForRehire && !t.IsDeleted)
            .OrderBy(t => t.EligibleForRehireDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineTermination>> GetPendingPaycheckProcessingAsync()
    {
        return await _dbSet
            .Include(t => t.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(t => !t.FinalPaycheckProcessed && !t.IsDeleted)
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

    public async Task<StaffDisciplineSeparation?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(s => s.ExitInterviewer)
            .Include(s => s.AccessRevokedBy)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(s => s.DisciplinaryActionId == caseId && !s.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetIncompleteAsync()
    {
        return await _dbSet
            .Include(s => s.ExitInterviewer)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(s => !s.ExitChecklistCompleted && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetByInterviewerAsync(Guid interviewerId)
    {
        return await _dbSet
            .Include(s => s.ExitInterviewer)
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(s => s.ExitInterviewerId == interviewerId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetPendingAccessRevocationAsync()
    {
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(s => !s.AccessRevoked && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineSeparation>> GetPendingEquipmentReturnAsync()
    {
        return await _dbSet
            .Include(s => s.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(s => !s.EquipmentReturned && !s.IsDeleted)
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

    public async Task<StaffDisciplineAppeal?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.AppealOutcomeBy)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .FirstOrDefaultAsync(a => a.DisciplinaryActionId == caseId && !a.IsDeleted);
    }

    public async Task<StaffDisciplineAppeal?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.AppealOutcomeBy)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Include(a => a.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByStatusAsync(DisciplineAppealStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(a => a.AppealStatus == status && !a.IsDeleted)
            .OrderByDescending(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetPendingHearingScheduleAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(a => !a.IsDeleted
                     && (a.AppealStatus == DisciplineAppealStatus.Filed
                         || a.AppealStatus == DisciplineAppealStatus.UnderReview)
                     && a.HearingDate == null)
            .OrderBy(a => a.FiledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetAwaitingOutcomeAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AppealOfficer)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(a => !a.IsDeleted
                     && a.HearingDate != null
                     && a.HearingDate < today
                     && a.AppealOutcome == null)
            .OrderBy(a => a.HearingDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineAppeal>> GetByAppealOfficerAsync(Guid officerId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(a => a.AppealOfficerId == officerId && !a.IsDeleted)
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

    public async Task<StaffDisciplineCorrectiveAction?> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.Items)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .FirstOrDefaultAsync(ca => ca.DisciplinaryActionId == caseId && !ca.IsDeleted);
    }

    public async Task<StaffDisciplineCorrectiveAction?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.Items)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .FirstOrDefaultAsync(ca => ca.Id == id && !ca.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(ca => ca.EmployeeId == employeeId && !ca.IsDeleted)
            .OrderByDescending(ca => ca.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetBySupervisorAsync(Guid supervisorId)
    {
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(ca => ca.SupervisorId == supervisorId && !ca.IsDeleted)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByStatusAsync(DisciplineCorrectiveActionStatus status)
    {
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(ca => ca.Status == status && !ca.IsDeleted)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(ca => !ca.IsDeleted
                      && ca.ReviewDate < today
                      && ca.Status != DisciplineCorrectiveActionStatus.Completed
                      && ca.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(ca => ca.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetDueForReviewAsync(int daysAhead = 14)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(ca => ca.Employee)
            .Include(ca => ca.Supervisor)
            .Include(ca => ca.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(ca => !ca.IsDeleted
                      && ca.ReviewDate >= DateTime.UtcNow
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

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetByCorrectiveActionIdAsync(Guid correctiveActionId)
    {
        return await _dbSet
            .Where(i => i.CorrectiveActionId == correctiveActionId && !i.IsDeleted)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetPendingByCorrectiveActionAsync(Guid correctiveActionId)
    {
        return await _dbSet
            .Where(i => i.CorrectiveActionId == correctiveActionId
                     && !i.IsDeleted
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueItemsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(i => i.CorrectiveAction).ThenInclude(ca => ca.Employee)
            .Include(i => i.CorrectiveAction).ThenInclude(ca => ca.DisciplinaryAction)
            .Where(i => !i.IsDeleted
                     && i.TargetDate < today
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueByCorrectiveActionAsync(Guid correctiveActionId)
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Where(i => i.CorrectiveActionId == correctiveActionId
                     && !i.IsDeleted
                     && i.TargetDate < today
                     && i.Status != DisciplineCorrectiveActionStatus.Completed
                     && i.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .OrderBy(i => i.TargetDate)
            .ToListAsync();
    }
}

#endregion
