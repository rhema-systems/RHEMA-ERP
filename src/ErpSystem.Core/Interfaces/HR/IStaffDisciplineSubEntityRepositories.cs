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
    Task<StaffDisciplineInvestigation?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns all open investigations assigned to the specified investigator.</summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetByInvestigatorAsync(Guid investigatorId);

    /// <summary>Returns investigations with no completion date (still in progress).</summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetOpenInvestigationsAsync();

    /// <summary>
    /// Returns open investigations that started more than <paramref name="maxDays"/> days ago
    /// without a completion date, ordered by start date ascending.
    /// </summary>
    Task<IEnumerable<StaffDisciplineInvestigation>> GetOverdueInvestigationsAsync(int maxDays = 30);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING
// ============================================================================

#region Staff Discipline Hearing

public interface IStaffDisciplineHearingRepository : IGenericRepository<StaffDisciplineHearing>
{
    /// <summary>Returns the hearing record for a case, or null if none has been scheduled.</summary>
    Task<StaffDisciplineHearing?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns all hearings where the specified employee is the hearing officer.</summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetByHearingOfficerAsync(Guid officerId);

    /// <summary>Returns hearings scheduled within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetUpcomingHearingsAsync(int daysAhead = 14);

    /// <summary>
    /// Returns hearings whose scheduled date has passed but no notes or outcome have been recorded,
    /// indicating the hearing result is still pending entry.
    /// </summary>
    Task<IEnumerable<StaffDisciplineHearing>> GetAwaitingOutcomeAsync();
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING
// ============================================================================

#region Staff Discipline Warning

public interface IStaffDisciplineWarningRepository : IGenericRepository<StaffDisciplineWarning>
{
    /// <summary>Returns the warning record for a case, or null if no warning penalty applies.</summary>
    Task<StaffDisciplineWarning?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns all warning penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns non-expired warning penalties for an employee (expiry date is null or in the future).</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetActiveWarningsForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all warnings of the specified type across all tenanted cases.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetByTypeAsync(DisciplinaryWarningType warningType);

    /// <summary>Returns warnings whose expiry date falls within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineWarning>> GetExpiringAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION
// ============================================================================

#region Staff Discipline Suspension

public interface IStaffDisciplineSuspensionRepository : IGenericRepository<StaffDisciplineSuspension>
{
    /// <summary>Returns the suspension record for a case, or null if no suspension penalty applies.</summary>
    Task<StaffDisciplineSuspension?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns all suspension penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns employees whose suspension is currently active (today falls within the start/end window).</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetCurrentlyActiveAsync();

    /// <summary>Returns suspensions starting within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineSuspension>> GetUpcomingAsync(int daysAhead = 7);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE
// ============================================================================

#region Staff Discipline Fine

public interface IStaffDisciplineFineRepository : IGenericRepository<StaffDisciplineFine>
{
    /// <summary>Returns the fine record for a case, or null if no fine penalty applies.</summary>
    Task<StaffDisciplineFine?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns all fine penalties across all cases for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns all fines that have not been fully paid (status is Pending or PartiallyPaid).</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetOutstandingAsync();

    /// <summary>Returns fines whose due date has passed and are not yet fully paid.</summary>
    Task<IEnumerable<StaffDisciplineFine>> GetOverdueAsync();

    /// <summary>
    /// Returns the total outstanding balance (fine amount minus payments) for an employee
    /// across all their disciplinary cases.
    /// </summary>
    Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid employeeId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION
// ============================================================================

#region Staff Discipline Termination

public interface IStaffDisciplineTerminationRepository : IGenericRepository<StaffDisciplineTermination>
{
    /// <summary>Returns the termination record for a case, or null if the case did not result in termination.</summary>
    Task<StaffDisciplineTermination?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns termination records filtered by termination type.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetByTypeAsync(EmployeeTerminationType type);

    /// <summary>Returns records where the employee is eligible for rehire.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetEligibleForRehireAsync();

    /// <summary>Returns records where the final paycheque has not yet been processed.</summary>
    Task<IEnumerable<StaffDisciplineTermination>> GetPendingPaycheckProcessingAsync();
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SEPARATION
// ============================================================================

#region Staff Discipline Separation

public interface IStaffDisciplineSeparationRepository : IGenericRepository<StaffDisciplineSeparation>
{
    /// <summary>Returns the separation checklist for a case, or null if none has been initiated.</summary>
    Task<StaffDisciplineSeparation?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns separation records where the exit checklist is not yet fully completed.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetIncompleteAsync();

    /// <summary>Returns separation records assigned to the specified exit interviewer.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetByInterviewerAsync(Guid interviewerId);

    /// <summary>Returns records where system access has not yet been revoked.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetPendingAccessRevocationAsync();

    /// <summary>Returns records where equipment return has not been confirmed.</summary>
    Task<IEnumerable<StaffDisciplineSeparation>> GetPendingEquipmentReturnAsync();
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL
// ============================================================================

#region Staff Discipline Appeal

public interface IStaffDisciplineAppealRepository : IGenericRepository<StaffDisciplineAppeal>
{
    /// <summary>Returns the single appeal for a case (at most one per case), or null if none filed.</summary>
    Task<StaffDisciplineAppeal?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns a fully-loaded appeal including officer, outcome officer, and attached documents.</summary>
    Task<StaffDisciplineAppeal?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns all appeals filed by an employee across all cases, newest first.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns appeals filtered by appeal status.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByStatusAsync(DisciplineAppealStatus status);

    /// <summary>Returns appeals in Filed or UnderReview status with no hearing date yet scheduled.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetPendingHearingScheduleAsync();

    /// <summary>Returns appeals where the hearing date has passed but no outcome has been recorded.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetAwaitingOutcomeAsync();

    /// <summary>Returns appeals assigned to the specified appeal officer.</summary>
    Task<IEnumerable<StaffDisciplineAppeal>> GetByAppealOfficerAsync(Guid officerId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION
// ============================================================================

#region Staff Discipline Corrective Action

public interface IStaffDisciplineCorrectiveActionRepository : IGenericRepository<StaffDisciplineCorrectiveAction>
{
    /// <summary>Returns the corrective action plan for a case, or null if none has been created.</summary>
    Task<StaffDisciplineCorrectiveAction?> GetByCaseIdAsync(Guid caseId);

    /// <summary>Returns a corrective action plan fully loaded with all its items.</summary>
    Task<StaffDisciplineCorrectiveAction?> GetWithItemsAsync(Guid id);

    /// <summary>Returns all corrective action plans for an employee, newest first.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns all corrective action plans supervised by the specified employee.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetBySupervisorAsync(Guid supervisorId);

    /// <summary>Returns corrective action plans filtered by status.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetByStatusAsync(DisciplineCorrectiveActionStatus status);

    /// <summary>Returns plans whose review date has passed and are not yet Completed or Cancelled.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetOverdueAsync();

    /// <summary>Returns plans whose review date falls within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveAction>> GetDueForReviewAsync(int daysAhead = 14);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION ITEM
// ============================================================================

#region Staff Discipline Corrective Action Item

public interface IStaffDisciplineCorrectiveActionItemRepository : IGenericRepository<StaffDisciplineCorrectiveActionItem>
{
    /// <summary>Returns all items for a corrective action plan, ordered by target date.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetByCorrectiveActionIdAsync(Guid correctiveActionId);

    /// <summary>Returns items in a plan that are still Pending or InProgress.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetPendingByCorrectiveActionAsync(Guid correctiveActionId);

    /// <summary>Returns all overdue items (target date passed, not Completed or Cancelled) across all plans.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueItemsAsync();

    /// <summary>Returns overdue items scoped to a specific corrective action plan.</summary>
    Task<IEnumerable<StaffDisciplineCorrectiveActionItem>> GetOverdueByCorrectiveActionAsync(Guid correctiveActionId);
}

#endregion
