using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// FR-HR-181 — employee grievances and their escalation through Employee → Supervisor → HOD → HR →
/// GM Finance &amp; Administration → Managing Director → Board.
/// </summary>
/// <remarks>
/// Deliberately NOT on the workflow engine: the engine models approval, and a grievance is answered
/// rather than approved. The decision to escalate belongs to the griever, not to an approver.
/// </remarks>
public interface IStaffGrievanceService
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Every grievance in the tenant. HR only — this is people's complaints about each other.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffGrievanceSummaryDto>> GetByStatusAsync(GrievanceStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// The employee-relations register — area 9c slice 1. HR only, and paged in the DATABASE.
    /// </summary>
    /// <remarks>
    /// <see cref="GetAllAsync"/> is kept for the callers that already have it, but it materialises
    /// every case in the tenant and every step of each. That was tolerable at 69 rows and is not the
    /// shape to build a register on — the area-25 compliance roster measured 2.27 MB before it was
    /// paged. This one counts and pages before it projects.
    /// </remarks>
    Task<PagedResult<StaffGrievanceSummaryDto>> GetPagedAsync(
        int page,
        int pageSize,
        EmployeeRelationsCaseType? caseType = null,
        GrievanceStatus? status = null,
        GrievanceEscalationLevel? level = null,
        Guid? organizationUnitId = null,
        bool? awaitingResponseOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>Grievances sitting unanswered at a given rung — HR's view of where the ladder is stuck.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingResponseAsync(GrievanceEscalationLevel? level = null, CancellationToken cancellationToken = default);

    /// <summary>The caller's own grievances. Token-derived; there is no id-bearing equivalent.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetMineAsync(Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Grievances whose current step names the caller as the responder.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingMyResponseAsync(Guid responderEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One grievance in full. Readable by the griever, by HR, and by anyone named on one of its
    /// steps — and nobody else. A grievance is usually ABOUT somebody, so the read surface is
    /// narrower than the rest of the module.
    /// </summary>
    Task<StaffGrievanceDto> GetByIdAsync(Guid id, Guid? callerEmployeeId, CancellationToken cancellationToken = default);

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <param name="grieverEmployeeId">The caller, from their token — never from the payload.</param>
    Task<StaffGrievanceDto> FileAsync(FileGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Names who should answer at the current rung. HR only.</summary>
    Task<StaffGrievanceDto> AssignCurrentStepAsync(Guid grievanceId, AssignGrievanceStepDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers at the current rung. Permitted to HR and to whoever the step names; refused to
    /// everyone else, including the griever — you cannot answer your own grievance.
    /// </summary>
    Task<StaffGrievanceDto> RespondAsync(Guid grievanceId, RespondToGrievanceDto dto, Guid responderEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the grievance up a rung. The GRIEVER'S act — refused to HR, because escalating is the
    /// employee saying the answer did not satisfy them, and nobody can say that for them.
    /// </summary>
    Task<StaffGrievanceDto> EscalateAsync(Guid grievanceId, EscalateGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>The griever withdraws. Also theirs alone.</summary>
    Task<StaffGrievanceDto> WithdrawAsync(Guid grievanceId, WithdrawGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    // ── Area 9c slice 1 — the wider employee-relations register ───────────────

    /// <summary>
    /// Opens a non-grievance employee-relations case. HR only.
    /// </summary>
    /// <remarks>
    /// ⚠ Refuses <see cref="EmployeeRelationsCaseType.Grievance"/>. A grievance is the employee's own
    /// act and this method takes an explicit employee id, so allowing it here would be exactly the
    /// raise-on-behalf-of that <see cref="FileAsync"/> is shaped to prevent.
    /// </remarks>
    Task<StaffGrievanceDto> OpenCaseAsync(OpenEmployeeRelationsCaseDto dto, Guid openedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Adds a party — respondent, representative, union official, witness, mediator. HR only.</summary>
    Task<StaffGrievanceDto> AddPartyAsync(Guid grievanceId, AddGrievancePartyDto dto, Guid addedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Stands a party down. Not a delete — the file must still read correctly. HR only.</summary>
    Task<StaffGrievanceDto> RemovePartyAsync(Guid grievanceId, Guid partyId, RemoveGrievancePartyDto dto, CancellationToken cancellationToken = default);

    // ── Area 9c slice 2 — FR-HR-181's missing artefacts ───────────────────────

    /// <summary>
    /// Obligation 5. Records or amends HR's formal reading of the case. HR only, and refused once
    /// the case is closed — an interpretation edited after the outcome is what makes it
    /// indefensible.
    /// </summary>
    Task<StaffGrievanceDto> RecordHrInterpretationAsync(Guid grievanceId, RecordHrInterpretationDto dto, Guid recordedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Obligation 7. Opens the investigation. One per case. HR only.</summary>
    Task<StaffGrievanceDto> OpenInvestigationAsync(Guid grievanceId, OpenGrievanceInvestigationDto dto, Guid openedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Updates an investigation that is still open. HR only.</summary>
    Task<StaffGrievanceDto> UpdateInvestigationAsync(Guid grievanceId, UpdateGrievanceInvestigationDto dto, CancellationToken cancellationToken = default);

    /// <summary>Concludes the investigation. Refused without findings. HR only.</summary>
    Task<StaffGrievanceDto> CompleteInvestigationAsync(Guid grievanceId, CompleteGrievanceInvestigationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obligation 8. Resolves the case by recording what was decided — the act that
    /// <c>respond(resolvesGrievance: true)</c> used to stand in for.
    /// </summary>
    /// <remarks>
    /// Permitted to HR and to whoever the current step names, exactly as answering is. Used a second
    /// time it fills in a <see cref="GrievanceResolutionOutcome.NotRecorded"/> outcome and does
    /// nothing else — a recorded decision is never amended.
    /// </remarks>
    Task<StaffGrievanceDto> ResolveAsync(Guid grievanceId, ResolveGrievanceDto dto, Guid decidedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a case that reached the Board and was answered without being resolved. HR only.
    /// </summary>
    /// <remarks>
    /// ⚠ The first writer <see cref="GrievanceStatus.Closed"/> has ever had. Before slice 2 such a
    /// case had no terminal state at all and sat <c>UnderReview</c> for ever.
    /// </remarks>
    Task<StaffGrievanceDto> CloseAsync(Guid grievanceId, CloseGrievanceDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The discipline reminder sweep — area 9 slice 8.
/// </summary>
/// <remarks>
/// Covers both halves of the area: the disciplinary clocks (written query, investigation, hearing,
/// appeal windows, corrective actions, warnings, fines) and grievances sitting at an unanswered rung.
/// Scoped rather than owned by the background host, so the daily sweep and the HR-gated run-now
/// endpoint execute exactly the same code — the SHE/movements structure.
/// </remarks>
public interface IDisciplineReminderService
{
    Task<DisciplineReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>What a sweep run at <paramref name="asOf"/> would fire. Reads only — claims nothing.</summary>
    Task<IEnumerable<DisciplineReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<DisciplineReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<DisciplineReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default);
}
