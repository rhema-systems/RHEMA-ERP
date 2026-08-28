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

    // ── Area 9c slice 3 — documents and the signed agreement ──────────────────

    /// <summary>
    /// Checks that a document may be placed where the caller says, BEFORE the file is uploaded.
    /// </summary>
    /// <remarks>
    /// ⚠ This exists because <c>HrAttachmentUpload.ExecuteAsync</c> wraps its <c>persist</c>
    /// callback in a catch that rolls the stored document back and answers a generic <b>500</b>.
    /// That is right for an unexpected failure and wrong for a business rule: every refusal raised
    /// inside <see cref="AddDocumentAsync"/> reached the caller as "An error occurred while adding
    /// the attachment", with no status and no message. Calling this first means a refused placement
    /// never stores a file, never scans one, and never needs rolling back — and the rule's own
    /// message and status reach the caller through the normal filter.
    ///
    /// <para>The same checks stay in <see cref="AddDocumentAsync"/> as well: this one is called by
    /// the controller, that one is the last word.</para>
    /// </remarks>
    Task ValidateDocumentPlacementAsync(Guid grievanceId, GrievanceDocumentScope scope, Guid? stepId, Guid? conferenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the attachment row for a file that has already been through the controlled upload
    /// gate. Called from the controller's <c>persist</c> callback, never directly.
    /// </summary>
    /// <remarks>
    /// ⚠ Takes the stored document's own values. Nothing here accepts a caller-supplied
    /// <c>filePath</c> — that shape stores no file and is the defect the upload gate exists to
    /// prevent.
    /// </remarks>
    Task<StaffGrievanceDocumentDto> AddDocumentAsync(
        Guid grievanceId,
        GrievanceDocumentScope scope,
        Guid? stepId,
        Guid? conferenceId,
        string? description,
        DateTime? agreementSignedDate,
        Guid uploadedByEmployeeId,
        string fileName,
        string filePath,
        long fileSize,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One document, for the download endpoint to stream. Applies the case's own read rule — the
    /// entitlement check is never the download helper's.
    /// </summary>
    Task<StaffGrievanceDocumentDto> GetDocumentAsync(Guid documentId, Guid? callerEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Removes a document. HR only, and refused on a case that is no longer open.</summary>
    Task<StaffGrievanceDto> DeleteDocumentAsync(Guid grievanceId, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The employee confirms the signed agreement. Theirs alone — refused to HR and to everyone else.
    /// </summary>
    Task<StaffGrievanceDto> AcceptAgreementAsync(Guid grievanceId, AcceptGrievanceAgreementDto dto, Guid employeeId, CancellationToken cancellationToken = default);

    // ── Area 9c slice 4 — conferencing, mediation, union consultation ─────────

    /// <summary>
    /// Convenes a meeting on the case — a case conference, a mediation, or FR-HR-181 obligation 6's
    /// union consultation. HR only.
    /// </summary>
    Task<StaffGrievanceDto> ScheduleConferenceAsync(Guid grievanceId, ScheduleGrievanceConferenceDto dto, Guid convenedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Amends a meeting that has not happened yet. A null field means "leave alone". HR only.</summary>
    Task<StaffGrievanceDto> UpdateConferenceAsync(Guid grievanceId, Guid conferenceId, UpdateGrievanceConferenceDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the meeting happened, with its outcome, its notes and who came. Refused without
    /// an outcome. HR, or whoever chairs it.
    /// </summary>
    Task<StaffGrievanceDto> HoldConferenceAsync(Guid grievanceId, Guid conferenceId, HoldGrievanceConferenceDto dto, Guid recordedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a meeting that has not happened. HR only.</summary>
    Task<StaffGrievanceDto> CancelConferenceAsync(Guid grievanceId, Guid conferenceId, CancelGrievanceConferenceDto dto, CancellationToken cancellationToken = default);

    /// <summary>Adds somebody to a meeting. HR only.</summary>
    Task<StaffGrievanceDto> AddConferenceAttendeeAsync(Guid grievanceId, Guid conferenceId, AddConferenceAttendeeDto dto, CancellationToken cancellationToken = default);

    /// <summary>Removes somebody from a meeting that has not happened yet. HR only.</summary>
    Task<StaffGrievanceDto> RemoveConferenceAttendeeAsync(Guid grievanceId, Guid conferenceId, Guid attendeeId, CancellationToken cancellationToken = default);

    // ── Cross-links (area 9c slice 9) ─────────────────────────────────────────

    /// <summary>
    /// Cross-references a case to a safety incident, a PIP or a disciplinary case. HR only.
    /// </summary>
    /// <remarks>
    /// Refused unless the source record is about somebody already named on the case — the primary
    /// party or an active party. That rule is what stops a cross-reference introducing a person the
    /// file does not name, and it is why the link block is meaningful rather than decorative.
    ///
    /// <para>⚠ Not state-gated, unlike every other write here: filing a cross-reference against a
    /// closed case is a normal and useful act. See the implementation for the reasoning.</para>
    /// </remarks>
    Task<StaffGrievanceDto> LinkSourceAsync(Guid grievanceId, LinkErCaseSourceDto dto, CancellationToken cancellationToken = default);

    /// <summary>Removes one cross-reference. HR only. Nothing in the source record changes.</summary>
    Task<StaffGrievanceDto> UnlinkSourceAsync(Guid grievanceId, EmployeeRelationsLinkSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reverse read: the employee-relations cases pointing at one source record.
    /// </summary>
    /// <remarks>
    /// For the "this incident has employee-relations cases" affordance on the source module's
    /// screen. Deliberately a subset of what the register already shows the same caller, so it
    /// grants nothing new — no statement, no artefacts.
    /// </remarks>
    Task<IEnumerable<ErLinkedCaseDto>> GetCasesForSourceAsync(EmployeeRelationsLinkSource source, Guid recordId, CancellationToken cancellationToken = default);
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
