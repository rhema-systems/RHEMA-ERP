using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINARY CASE SERVICE
// ============================================================================

#region Staff Disciplinary Case Service

/// <summary>
/// Manages the full lifecycle of a disciplinary case: CRUD, workflow transitions,
/// and the module-level dashboard. Penalty sub-entities (Warning, Suspension, etc.)
/// are managed by their own dedicated services.
/// </summary>
public interface IStaffDisciplinaryCaseService
{
    // ── Queries ───────────────────────────────────────────────────────────────

    Task<StaffDisciplinaryActionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffDisciplinaryActionDto?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetAllOpenAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StaffDisciplinaryActionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByStatusAsync(DisciplinaryStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByOffenseAsync(Guid offenseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetBySeverityAsync(StaffOffenseSeverity minimumSeverity, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByIncidentDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByReportedDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingInvestigationAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingHearingAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingClosureAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveWarningAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveSuspensionAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithOutstandingFineAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithPendingTerminationAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveAppealAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveLegalReviewAsync(CancellationToken cancellationToken = default);
    Task<int> GetOpenCaseCountForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId, CancellationToken cancellationToken = default);

    // ── CRUD ─────────────────────────────────────────────────────────────────

    Task<StaffDisciplinaryActionDto> CreateAsync(CreateStaffDisciplinaryActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffDisciplinaryActionDto> UpdateAsync(UpdateStaffDisciplinaryActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Workflow transitions ──────────────────────────────────────────────────

    /// <summary>Advances a Draft case to Reported status (formally submitting the incident report).</summary>
    Task<bool> SubmitAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Advances a Reported case to UnderReview (HR has acknowledged and started review).</summary>
    Task<bool> StartReviewAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the formal disciplinary decision (action type, details, rationale) and
    /// advances the case to AwaitingDecision. Penalty sub-entities are created via their
    /// own services after this call.
    /// </summary>
    /// <param name="decidedByEmployeeId">
    /// The deciding officer, taken from the caller's token — never from the request body. Who decided
    /// a disciplinary case is testimony.
    /// </param>
    /// <remarks>
    /// Proposes the sanction and starts the approval instance. The case sits at AwaitingDecision
    /// until an approver confirms it. Enforces FR-HR-080: a non-HR caller may only issue an action
    /// whose MinimumAuthority is HeadOfDepartment.
    /// </remarks>
    Task<bool> RecordDecisionAsync(RecordDisciplinaryDecisionDto dto, Guid decidedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How a case stands against FR-HR-177's 48-hour written query and FR-HR-178's four-week
    /// investigation — both advisory — and whether the natural-justice gate currently permits a
    /// decision, which is not advisory.
    /// </summary>
    Task<DisciplineProcessClockDto> GetProcessClockAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the employee could not be given a chance to answer the written query — absconded,
    /// detained, refused service — so a decision may proceed without it. Requires a reason, which
    /// goes on the case record.
    /// </summary>
    Task<bool> WaiveQueryOpportunityAsync(Guid caseId, Guid waivedByEmployeeId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cases whose proposed decision the caller can confirm right now. Token-derived; the engine
    /// decides what is in it.
    /// </summary>
    Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetAwaitingMyApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirms the proposed decision, advancing the case to DecisionMade when the route completes.</summary>
    Task<bool> ApproveDecisionAsync(Guid caseId, Guid approvingEmployeeId, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses the proposed decision. The case returns to UnderReview with the proposed sanction
    /// cleared — refusing a sanction does not refuse the allegation.
    /// </summary>
    Task<bool> RejectDecisionAsync(Guid caseId, Guid rejectingEmployeeId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>The proposing officer takes their own decision back before anyone rules on it.</summary>
    Task<bool> RecallDecisionAsync(Guid caseId, Guid recallingEmployeeId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>Closes a case that is in AwaitingDecision or DecisionMade status.</summary>
    /// <param name="closedByEmployeeId">Taken from the caller's token, never from the request body.</param>
    Task<bool> CloseCaseAsync(CloseDisciplinaryCaseDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Transitions any non-terminal case to OnHold.</summary>
    Task<bool> PutOnHoldAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Reactivates an OnHold case back to UnderReview.</summary>
    Task<bool> ReactivateCaseAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Dismisses a case still in early stages (Draft, Reported, or UnderReview).</summary>
    Task<bool> DismissCaseAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    Task<StaffDisciplineDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

#endregion
