using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION SERVICE
// ============================================================================

#region Staff Discipline Investigation Service

public interface IStaffDisciplineInvestigationService
{
    Task<StaffDisciplineInvestigationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineInvestigationDto>> GetByInvestigatorAsync(Guid investigatorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOpenInvestigationsAsync(CancellationToken cancellationToken = default);
    /// <param name="maxDays">
    /// Defaults to the tenant's <c>CompanyHrPolicySettings.InvestigationDays</c>, which starts at
    /// FR-HR-178's four weeks.
    /// Pass a value only for an ad-hoc wider sweep — the queue and the case advisory must otherwise
    /// answer "overdue" the same way.
    /// </param>
    Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOverdueInvestigationsAsync(int? maxDays = null, CancellationToken cancellationToken = default);

    /// <summary>Opens an investigation for a case and advances its status to UnderInvestigation.</summary>
    Task<StaffDisciplineInvestigationDto> OpenAsync(OpenInvestigationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineInvestigationDto> UpdateAsync(UpdateInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks the investigation complete and advances the parent case status to InvestigationComplete.</summary>
    Task<bool> CompleteAsync(Guid caseId, string findings, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING SERVICE
// ============================================================================

#region Staff Discipline Hearing Service

public interface IStaffDisciplineHearingService
{
    Task<StaffDisciplineHearingDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineHearingDto>> GetByHearingOfficerAsync(Guid officerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineHearingDto>> GetUpcomingHearingsAsync(int daysAhead = 14, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineHearingDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules a hearing for a case and advances its status to HearingScheduled.</summary>
    Task<StaffDisciplineHearingDto> ScheduleAsync(ScheduleHearingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Records the hearing outcome and advances the parent case status to HearingConducted.</summary>
    Task<StaffDisciplineHearingDto> RecordOutcomeAsync(RecordHearingOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING SERVICE
// ============================================================================

#region Staff Discipline Warning Service

public interface IStaffDisciplineWarningService
{
    Task<StaffDisciplineWarningDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWarningDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWarningDto>> GetActiveWarningsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWarningDto>> GetByTypeAsync(DisciplinaryWarningType warningType, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWarningDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    /// <summary>Records a warning penalty for the case. Throws if a warning already exists for the case.</summary>
    Task<StaffDisciplineWarningDto> RecordAsync(RecordWarningPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineWarningDto> UpdateAsync(UpdateWarningPenaltyDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION SERVICE
// ============================================================================

#region Staff Discipline Suspension Service

public interface IStaffDisciplineSuspensionService
{
    Task<StaffDisciplineSuspensionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineSuspensionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineSuspensionDto>> GetCurrentlyActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineSuspensionDto>> GetUpcomingAsync(int daysAhead = 7, CancellationToken cancellationToken = default);

    /// <summary>Records a suspension penalty for the case. Throws if a suspension already exists.</summary>
    Task<StaffDisciplineSuspensionDto> RecordAsync(RecordSuspensionPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineSuspensionDto> UpdateAsync(UpdateSuspensionPenaltyDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE SERVICE
// ============================================================================

#region Staff Discipline Fine Service

public interface IStaffDisciplineFineService
{
    Task<StaffDisciplineFineDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineFineDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineFineDto>> GetOutstandingAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineFineDto>> GetOverdueAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Records a fine penalty for the case. Throws if a fine already exists.</summary>
    Task<StaffDisciplineFineDto> RecordAsync(RecordFinePenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Records a payment against the outstanding fine, updating the payment status and paid amount.</summary>
    Task<StaffDisciplineFineDto> RecordPaymentAsync(RecordFinePaymentDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL SERVICE
// ============================================================================

#region Staff Discipline Appeal Service

public interface IStaffDisciplineAppealService
{
    Task<StaffDisciplineAppealDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineAppealDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineAppealDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineAppealDto>> GetByStatusAsync(DisciplineAppealStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineAppealDto>> GetPendingHearingScheduleAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineAppealDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Files an appeal for the case and advances the case status to UnderAppeal.</summary>
    /// <param name="appellantEmployeeId">
    /// The caller, from their token. The appeal is refused unless this is the employee the case was
    /// brought against — an appeal is the subject's own act, so nobody may file one for them.
    /// </param>
    Task<StaffDisciplineAppealDto> FileAsync(FileAppealDto dto, Guid tenantId, Guid appellantEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Schedules the appeal hearing and advances the appeal status to HearingScheduled.</summary>
    Task<bool> ScheduleHearingAsync(ScheduleAppealHearingDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Records the appeal outcome and advances the appeal status to DecisionMade.</summary>
    /// <param name="userId">The deciding officer, from their token — stamped as AppealOutcomeById.</param>
    Task<bool> RecordOutcomeAsync(RecordAppealOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION SERVICE
// ============================================================================

#region Staff Discipline Corrective Action Service

public interface IStaffDisciplineCorrectiveActionService
{
    Task<StaffDisciplineCorrectiveActionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineCorrectiveActionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetBySupervisorAsync(Guid supervisorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByStatusAsync(DisciplineCorrectiveActionStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetOverdueAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetDueForReviewAsync(int daysAhead = 14, CancellationToken cancellationToken = default);

    Task<StaffDisciplineCorrectiveActionDto> CreateAsync(CreateStaffDisciplineCorrectiveActionDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineCorrectiveActionDto> UpdateAsync(UpdateStaffDisciplineCorrectiveActionDto updateDto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks the corrective action plan as Completed.</summary>
    Task<bool> CompleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Item operations
    Task<StaffDisciplineCorrectiveActionItemDto> AddItemAsync(CreateStaffDisciplineCorrectiveActionItemDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineCorrectiveActionItemDto> UpdateItemAsync(UpdateStaffDisciplineCorrectiveActionItemDto updateDto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a corrective action item as Completed and records the completion notes.</summary>
    Task<bool> CompleteItemAsync(Guid itemId, string completionNotes, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION SERVICE
// ============================================================================

#region Staff Discipline Termination Service

public interface IStaffDisciplineTerminationService
{
    Task<StaffDisciplineTerminationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineTerminationDto>> GetByTypeAsync(EmployeeTerminationType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineTerminationDto>> GetEligibleForRehireAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineTerminationDto>> GetPendingPaycheckProcessingAsync(CancellationToken cancellationToken = default);

    /// <summary>Records termination details for a case. Throws if a termination record already exists.</summary>
    Task<StaffDisciplineTerminationDto> RecordAsync(RecordTerminationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineTerminationDto> UpdateAsync(UpdateTerminationDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Separation operations
    Task<StaffDisciplineSeparationDto?> GetSeparationByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineSeparationDto>> GetIncompleteSeparationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Initiates the separation/offboarding checklist for a terminated employee.</summary>
    Task<StaffDisciplineSeparationDto> InitiateSeparationAsync(InitiateSeparationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineSeparationDto> UpdateSeparationAsync(UpdateSeparationDto dto, Guid userId, CancellationToken cancellationToken = default);
}

#endregion
