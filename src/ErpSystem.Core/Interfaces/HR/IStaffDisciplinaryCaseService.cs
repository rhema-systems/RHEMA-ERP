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
    Task<bool> RecordDecisionAsync(RecordDisciplinaryDecisionDto dto, CancellationToken cancellationToken = default);

    /// <summary>Closes a case that is in AwaitingDecision or DecisionMade status.</summary>
    Task<bool> CloseCaseAsync(CloseDisciplinaryCaseDto dto, CancellationToken cancellationToken = default);

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
