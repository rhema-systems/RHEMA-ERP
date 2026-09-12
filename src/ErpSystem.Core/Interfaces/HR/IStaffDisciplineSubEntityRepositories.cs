using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION
// ============================================================================

#region Staff Discipline Investigation

public interface IStaffDisciplineInvestigationRepository : IGenericRepository<StaffDisciplineInvestigation>
{
    /// <summary>Returns the investigation for a case, or null if none has been opened.</summary>
    Task<StaffDisciplineInvestigation?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all open investigations assigned to the specified investigator.</summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetByInvestigatorAsync(Guid tenantId, Guid investigatorId);

    /// <summary>Returns investigations with no completion date (still in progress).</summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetOpenInvestigationsAsync(Guid tenantId);

    /// <summary>
    /// Returns open investigations that started more than <paramref name="maxDays"/> days ago
    /// without a completion date, ordered by start date ascending.
    /// </summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetOverdueInvestigationsAsync(Guid tenantId, int maxDays = 30);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING
// ============================================================================

#region Staff Discipline Hearing

public interface IStaffDisciplineHearingRepository : IGenericRepository<StaffDisciplineHearing>
{
    /// <summary>Returns the hearing record for a case, or null if none has been scheduled.</summary>
    Task<StaffDisciplineHearing?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all hearings where the specified employee is the hearing officer.</summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetByHearingOfficerAsync(Guid tenantId, Guid officerId);

    /// <summary>Returns hearings scheduled within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetUpcomingHearingsAsync(Guid tenantId, int daysAhead = 14);

    /// <summary>
    /// Returns hearings whose scheduled date has passed but no notes or outcome have been recorded,
    /// indicating the hearing result is still pending entry.
    /// </summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetAwaitingOutcomeAsync(Guid tenantId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING
// ============================================================================

#region Staff Discipline Warning

public interface IStaffDisciplineWarningRepository : IGenericRepository<StaffDisciplineWarning>
{
    /// <summary>Returns the warning record for a case, or null if no warning penalty applies.</summary>
    Task<StaffDisciplineWarning?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all warning penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns non-expired warning penalties for an employee (expiry date is null or in the future).</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetActiveWarningsForEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns all warnings of the specified type across all tenanted cases.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetByTypeAsync(Guid tenantId, DisciplinaryWarningType warningType);

    /// <summary>Returns warnings whose expiry date falls within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetExpiringAsync(Guid tenantId, int daysAhead = 30);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION
// ============================================================================

#region Staff Discipline Suspension

public interface IStaffDisciplineSuspensionRepository : IGenericRepository<StaffDisciplineSuspension>
{
    /// <summary>Returns the suspension record for a case, or null if no suspension penalty applies.</summary>
    Task<StaffDisciplineSuspension?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all suspension penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns employees whose suspension is currently active (today falls within the start/end window).</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetCurrentlyActiveAsync(Guid tenantId);

    /// <summary>Returns suspensions starting within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetUpcomingAsync(Guid tenantId, int daysAhead = 7);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE
// ============================================================================

#region Staff Discipline Fine

public interface IStaffDisciplineFineRepository : IGenericRepository<StaffDisciplineFine>
{
    /// <summary>Returns the fine record for a case, or null if no fine penalty applies.</summary>
    Task<StaffDisciplineFine?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all fine penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns all fines that have not been fully paid (status is Pending or PartiallyPaid).</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetOutstandingAsync(Guid tenantId);

    /// <summary>Returns fines whose due date has passed and are not yet fully paid.</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetOverdueAsync(Guid tenantId);

    /// <summary>
    /// Returns the total outstanding balance (fine amount minus payments) for an employee
    /// across all their disciplinary cases.
    /// </summary>
    Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid tenantId, Guid employeeId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION
// ============================================================================

#region Staff Discipline Termination

public interface IStaffDisciplineTerminationRepository : IGenericRepository<StaffDisciplineTermination>
{
    /// <summary>Returns the termination record for a case, or null if the case did not result in termination.</summary>
    Task<StaffDisciplineTermination?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns termination records filtered by termination type.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetByTypeAsync(Guid tenantId, EmployeeTerminationType type);

    /// <summary>Returns records where the employee is eligible for rehire.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetEligibleForRehireAsync(Guid tenantId);

    /// <summary>Returns records where the final paycheque has not yet been processed.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetPendingPaycheckProcessingAsync(Guid tenantId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SEPARATION
// ============================================================================

#region Staff Discipline Separation

public interface IStaffDisciplineSeparationRepository : IGenericRepository<StaffDisciplineSeparation>
{
    /// <summary>Returns the separation checklist for a case, or null if none has been initiated.</summary>
    Task<StaffDisciplineSeparation?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns separation records where the exit checklist is not yet fully completed.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetIncompleteAsync(Guid tenantId);

    /// <summary>Returns separation records assigned to the specified exit interviewer.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetByInterviewerAsync(Guid tenantId, Guid interviewerId);

    /// <summary>Returns records where system access has not yet been revoked.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetPendingAccessRevocationAsync(Guid tenantId);

    /// <summary>Returns records where equipment return has not been confirmed.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetPendingEquipmentReturnAsync(Guid tenantId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL
// ============================================================================

#region Staff Discipline Appeal

public interface IStaffDisciplineAppealRepository : IGenericRepository<StaffDisciplineAppeal>
{
    /// <summary>Returns the single appeal for a case (at most one per case), or null if none filed.</summary>
    Task<StaffDisciplineAppeal?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns a fully-loaded appeal including officer, outcome officer, and attached documents.</summary>
    Task<StaffDisciplineAppeal?> GetWithFullDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all appeals filed by an employee across all cases, newest first.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns appeals filtered by appeal status.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByStatusAsync(Guid tenantId, DisciplineAppealStatus status);

    /// <summary>Returns appeals in Filed or UnderReview status with no hearing date yet scheduled.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetPendingHearingScheduleAsync(Guid tenantId);

    /// <summary>Returns appeals where the hearing date has passed but no outcome has been recorded.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetAwaitingOutcomeAsync(Guid tenantId);

    /// <summary>Returns appeals assigned to the specified appeal officer.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByAppealOfficerAsync(Guid tenantId, Guid officerId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION
// ============================================================================

#region Staff Discipline Corrective Action

public interface IStaffDisciplineCorrectiveActionRepository : IGenericRepository<StaffDisciplineCorrectiveAction>
{
    /// <summary>Returns the corrective action plan for a case, or null if none has been created.</summary>
    Task<StaffDisciplineCorrectiveAction?> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns a corrective action plan fully loaded with all its items.</summary>
    Task<StaffDisciplineCorrectiveAction?> GetWithItemsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all corrective action plans for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns all corrective action plans supervised by the specified employee.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetBySupervisorAsync(Guid tenantId, Guid supervisorId);

    /// <summary>Returns corrective action plans filtered by status.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByStatusAsync(Guid tenantId, DisciplineCorrectiveActionStatus status);

    /// <summary>Returns plans whose review date has passed and are not yet Completed or Cancelled.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetOverdueAsync(Guid tenantId);

    /// <summary>Returns plans whose review date falls within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetDueForReviewAsync(Guid tenantId, int daysAhead = 14);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION ITEM
// ============================================================================

#region Staff Discipline Corrective Action Item

public interface IStaffDisciplineCorrectiveActionItemRepository : IGenericRepository<StaffDisciplineCorrectiveActionItem>
{
    /// <summary>Returns all items for a corrective action plan, ordered by target date.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetByCorrectiveActionIdAsync(Guid tenantId, Guid correctiveActionId);

    /// <summary>Returns items in a plan that are still Pending or InProgress.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetPendingByCorrectiveActionAsync(Guid tenantId, Guid correctiveActionId);

    /// <summary>Returns all overdue items (target date passed, not Completed or Cancelled) across all plans.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueItemsAsync(Guid tenantId);

    /// <summary>Returns overdue items scoped to a specific corrective action plan.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueByCorrectiveActionAsync(Guid tenantId, Guid correctiveActionId);
}

#endregion
