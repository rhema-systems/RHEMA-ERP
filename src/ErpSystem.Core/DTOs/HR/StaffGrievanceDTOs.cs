using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF GRIEVANCE DTOs — FR-HR-181
// ============================================================================

#region Staff Grievance

public class StaffGrievanceStepDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }
    public GrievanceEscalationLevel Level { get; set; }
    public string LevelName => Level.ToString();
    public int Sequence { get; set; }
    public DateTime ReachedDate { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? Response { get; set; }
    public DateTime? RespondedDate { get; set; }
    public Guid? RespondedById { get; set; }
    public string? RespondedByName { get; set; }
    public GrievanceStepOutcome Outcome { get; set; }
    public string OutcomeName => Outcome.ToString();
}

/// <summary>Somebody on the case besides the primary party — area 9c slice 1.</summary>
public class StaffGrievancePartyDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }
    public GrievancePartyRole Role { get; set; }
    public string RoleName => Role.ToString();

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalOrganisation { get; set; }

    /// <summary>Resolved display name, whichever kind of party this is. The list renders this.</summary>
    public string DisplayName => EmployeeName ?? ExternalName ?? string.Empty;

    public Guid? RepresentsEmployeeId { get; set; }
    public string? RepresentsEmployeeName { get; set; }
    public Guid? UnionId { get; set; }
    public string? UnionName { get; set; }

    public DateTime AddedDate { get; set; }
    public Guid? AddedById { get; set; }
    public string? AddedByName { get; set; }
    public string? Notes { get; set; }

    public DateTime? RemovedDate { get; set; }
    public string? RemovalReason { get; set; }

    /// <summary>False once the party has stood down. The case file still shows them.</summary>
    public bool IsActive => RemovedDate == null;
}

public class StaffGrievanceSummaryDto
{
    public Guid Id { get; set; }
    public string GrievanceNumber { get; set; } = string.Empty;

    /// <summary>Area 9c: which kind of employee-relations case this is. Defaults to Grievance.</summary>
    public EmployeeRelationsCaseType CaseType { get; set; }
    public string CaseTypeName => CaseType.ToString();

    /// <summary>The primary party.</summary>
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime FiledDate { get; set; }
    public GrievanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public GrievanceEscalationLevel CurrentLevel { get; set; }
    public string CurrentLevelName => CurrentLevel.ToString();

    /// <summary>True while the rung the grievance sits at has not answered.</summary>
    public bool AwaitingResponse { get; set; }

    /// <summary>
    /// How many parties are currently on the case besides the primary one. A count rather than the
    /// rows: the register must not carry who is involved in every case on the page.
    /// </summary>
    public int ActivePartyCount { get; set; }
}

public class StaffGrievanceDto : StaffGrievanceSummaryDto
{
    public Guid TenantId { get; set; }
    public string? EmployeeNumber { get; set; }

    /// <summary>The employee's own words. Never amended after filing — FR-HR-181 requires it retained.</summary>
    public string Statement { get; set; } = string.Empty;

    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionSummary { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public string? WithdrawalReason { get; set; }

    /// <summary>The full ladder in order — every rung reached, answered or not.</summary>
    public List<StaffGrievanceStepDto> Steps { get; set; } = new();

    /// <summary>
    /// Everybody on the case besides the primary party, those who have stood down included —
    /// area 9c slice 1. Ordered by role then by when they were added, so the file reads the same
    /// way twice.
    /// </summary>
    public List<StaffGrievancePartyDto> Parties { get; set; } = new();

    // ── FR-HR-181's artefacts (area 9c slice 2) ──────────────────────────────

    /// <summary>Obligation 5 — HR's formal reading of the case, distinct from the HR rung's answer.</summary>
    public string? HrInterpretation { get; set; }
    public Guid? HrInterpretationById { get; set; }
    public string? HrInterpretationByName { get; set; }
    public DateTime? HrInterpretationDate { get; set; }

    /// <summary>Obligation 7 — the investigation report, if one was opened.</summary>
    public StaffGrievanceInvestigationDto? Investigation { get; set; }

    /// <summary>Obligation 8 — the resolution decision, if the case has been resolved.</summary>
    public StaffGrievanceResolutionDto? Resolution { get; set; }

    public DateTime? ClosedDate { get; set; }
    public string? ClosureReason { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }

    /// <summary>
    /// Obligation 9 and the case's paperwork — area 9c slice 3. Ordered by scope then upload date,
    /// so the file reads the same way twice.
    /// </summary>
    public List<StaffGrievanceDocumentDto> Documents { get; set; } = new();

    /// <summary>
    /// Conferences, mediations and union consultations — area 9c slice 4, in the order they were
    /// scheduled. Their notes are redacted per reader; see <see cref="StaffGrievanceConferenceDto"/>.
    /// </summary>
    public List<StaffGrievanceConferenceDto> Conferences { get; set; } = new();
}

/// <summary>FR-HR-181 obligation 7 — the investigation report.</summary>
public class StaffGrievanceInvestigationDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }

    public Guid? InvestigatorId { get; set; }
    public string? InvestigatorName { get; set; }
    public string? ExternalInvestigatorName { get; set; }
    public string? ExternalInvestigatorOrganisation { get; set; }

    /// <summary>Resolved display name, internal or external. The screen renders this.</summary>
    public string InvestigatorDisplayName => InvestigatorName ?? ExternalInvestigatorName ?? string.Empty;

    public DateTime StartedDate { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public string? Findings { get; set; }
    public string? EvidenceCollected { get; set; }
    public string? Recommendation { get; set; }

    public Guid? OpenedById { get; set; }
    public string? OpenedByName { get; set; }

    /// <summary>An investigation is complete when it has been concluded, never by a separate flag.</summary>
    public bool IsComplete => CompletedDate != null;

    /// <summary>True when it is past its target and still open. What the slice-7 sweep chases.</summary>
    public bool IsOverdue => CompletedDate == null && TargetDate != null && TargetDate < DateTime.UtcNow;
}

/// <summary>FR-HR-181 obligation 8 — the resolution decision.</summary>
public class StaffGrievanceResolutionDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }

    public GrievanceResolutionOutcome Outcome { get; set; }
    public string OutcomeName => Outcome.ToString();

    /// <summary>
    /// True while the case was resolved but nobody captured WHAT was decided — the legacy
    /// <c>respond(resolvesGrievance: true)</c> path. HR can still fill it in; nothing else about a
    /// recorded decision may be changed.
    /// </summary>
    public bool OutcomeIsMissing => Outcome == GrievanceResolutionOutcome.NotRecorded;

    public string Decision { get; set; } = string.Empty;
    public string? RemedyOrUndertakings { get; set; }

    public Guid DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime DecidedDate { get; set; }

    public GrievanceEscalationLevel DecidedAtLevel { get; set; }
    public string DecidedAtLevelName => DecidedAtLevel.ToString();

    public DateTime? OutcomeRecordedDate { get; set; }
    public string? OutcomeRecordedByName { get; set; }

    // FR-HR-181 obligation 9 — area 9c slice 3.
    public DateTime? AgreementSignedDate { get; set; }
    public DateTime? AgreementAcceptedDate { get; set; }
    public Guid? AgreementAcceptedById { get; set; }
    public string? AgreementAcceptedByName { get; set; }

    public string? AgreementAcceptanceComment { get; set; }

    /// <summary>True once the employee has confirmed the agreement in the system.</summary>
    public bool AgreementAccepted => AgreementAcceptedDate != null;
}

/// <summary>Somebody asked to a conference — area 9c slice 4.</summary>
public class StaffGrievanceConferenceAttendeeDto
{
    public Guid Id { get; set; }
    public Guid ConferenceId { get; set; }

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalOrganisation { get; set; }

    public string DisplayName => EmployeeName ?? ExternalName ?? string.Empty;

    public string? Capacity { get; set; }

    /// <summary>Null until recorded — <c>false</c> means asked and did not come, which is a fact.</summary>
    public bool? DidAttend { get; set; }
    public string? ApologyReason { get; set; }
}

/// <summary>
/// A meeting convened on the case — case conference, mediation, or FR-HR-181 obligation 6's union
/// consultation. Area 9c slice 4.
/// </summary>
public class StaffGrievanceConferenceDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }

    public GrievanceConferenceType ConferenceType { get; set; }
    public string ConferenceTypeName => ConferenceType.ToString();

    public GrievanceConferenceStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime ScheduledFor { get; set; }
    public string? Venue { get; set; }

    public Guid? ChairId { get; set; }
    public string? ChairName { get; set; }
    public string? ExternalChairName { get; set; }
    public string? ExternalChairOrganisation { get; set; }
    public string ChairDisplayName => ChairName ?? ExternalChairName ?? string.Empty;

    public Guid? UnionId { get; set; }
    public string? UnionName { get; set; }

    public string? Purpose { get; set; }

    /// <summary>
    /// ⚠ <b>Null for everyone but HR and the chair</b>, even when notes exist — see
    /// <see cref="NotesRedacted"/>. A mediation's notes record what the other party said in a room
    /// they were promised was private, and the case's read rule admits the complainant.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>True when notes exist but this reader may not see them. Not the same as "no notes".</summary>
    public bool NotesRedacted { get; set; }

    /// <summary>What the meeting concluded. Visible to anyone who may read the case.</summary>
    public string? Outcome { get; set; }

    public DateTime? HeldDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }

    public Guid? ConvenedById { get; set; }
    public string? ConvenedByName { get; set; }

    public List<StaffGrievanceConferenceAttendeeDto> Attendees { get; set; } = new();
}

/// <summary>A document on an employee-relations case — area 9c slice 3.</summary>
public class StaffGrievanceDocumentDto
{
    public Guid Id { get; set; }
    public Guid GrievanceId { get; set; }

    public GrievanceDocumentScope Scope { get; set; }
    public string ScopeName => Scope.ToString();

    public Guid? StepId { get; set; }

    /// <summary>Set only when <see cref="Scope"/> is <c>Conference</c> — area 9c slice 4.</summary>
    public Guid? ConferenceId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// ⚠ A stored location, NEVER a URL. The file lives outside the web root and reading it needs
    /// the bearer token, so the UI must go through the download endpoint — an anchor href cannot
    /// work. Carried only so the desk can see where a document was filed.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }

    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }
}

/// <summary>
/// Raises a grievance.
/// </summary>
/// <remarks>
/// There is no employee id here. The griever is the caller — taken from the token — because a
/// grievance is the employee's own act, the same reasoning that keeps an appeal off anyone else's
/// hands. HR cannot raise one on somebody's behalf.
/// </remarks>
public class FileGrievanceDto
{
    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(6000)]
    [MinLength(20)]
    public string Statement { get; set; } = string.Empty;
}

/// <summary>
/// Answers the grievance at the rung it currently sits at.
/// </summary>
/// <remarks>
/// The responder is the caller, from their token. <c>Escalate</c> is what moves it up the ladder —
/// and the decision to escalate belongs to the GRIEVER, so a responder who cannot resolve it records
/// their answer and the employee decides whether that settles it.
/// </remarks>
public class RespondToGrievanceDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(10)]
    public string Response { get; set; } = string.Empty;

    /// <summary>True when this answer settles the grievance; false leaves it open for the employee.</summary>
    public bool ResolvesGrievance { get; set; }
}

/// <summary>Names who should answer at the current rung, so they can see and answer it.</summary>
public class AssignGrievanceStepDto
{
    [Required]
    public Guid AssignedToId { get; set; }
}

/// <summary>The employee escalates because the answer they were given did not settle it.</summary>
public class EscalateGrievanceDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class WithdrawGrievanceDto
{
    [Required]
    [MaxLength(1000)]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}

// ── Area 9c slice 1 — the employee-relations register ────────────────────────

/// <summary>
/// Opens an employee-relations case that is NOT a grievance — a mediation, a welfare matter, a
/// union consultation.
/// </summary>
/// <remarks>
/// ⚠ This is HR's act and takes an explicit <see cref="EmployeeId"/>, which is the exact opposite of
/// <see cref="FileGrievanceDto"/> and deliberately so. A grievance is the employee's own complaint
/// and nobody may raise one for them; a mediation or a welfare case is opened by the desk ABOUT
/// somebody, the way a disciplinary case is. The service refuses
/// <see cref="EmployeeRelationsCaseType.Grievance"/> here for that reason — it would be a
/// raise-on-behalf-of by the back door.
/// </remarks>
public class OpenEmployeeRelationsCaseDto
{
    [Required]
    public EmployeeRelationsCaseType CaseType { get; set; }

    /// <summary>The primary party — whom the case is about.</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(6000)]
    [MinLength(20)]
    public string Statement { get; set; } = string.Empty;
}

/// <summary>Adds somebody to a case. HR's act.</summary>
/// <remarks>
/// Exactly one of <see cref="EmployeeId"/> and <see cref="ExternalName"/> must be supplied —
/// representation is very often by a union official or a lawyer who is not on the payroll.
/// </remarks>
public class AddGrievancePartyDto
{
    [Required]
    public GrievancePartyRole Role { get; set; }

    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(200)]
    public string? ExternalOrganisation { get; set; }

    /// <summary>Whom they act for. Only meaningful for a representative.</summary>
    public Guid? RepresentsEmployeeId { get; set; }

    /// <summary>The union they act for. Only meaningful for a union representative.</summary>
    public Guid? UnionId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Stands a party down. Not a delete — the case file must still read correctly.</summary>
public class RemoveGrievancePartyDto
{
    [Required]
    [MaxLength(500)]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}

// ── Area 9c slice 2 — FR-HR-181's missing artefacts ──────────────────────────

/// <summary>Records or amends HR's formal reading of the case. HR only, and only while it is open.</summary>
public class RecordHrInterpretationDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(20)]
    public string Interpretation { get; set; } = string.Empty;
}

/// <summary>
/// Opens an investigation. Exactly one of the investigator and the external name is supplied — a
/// grievance about senior management is exactly the one that gets an outside investigator.
/// </summary>
public class OpenGrievanceInvestigationDto
{
    public Guid? InvestigatorId { get; set; }

    [MaxLength(200)]
    public string? ExternalInvestigatorName { get; set; }

    [MaxLength(200)]
    public string? ExternalInvestigatorOrganisation { get; set; }

    /// <summary>
    /// When it is due. ⚠ There is no statutory grievance clock — FR-HR-178's four weeks is the
    /// DISCIPLINARY investigation and must not be applied here by default. Left to the desk.
    /// </summary>
    public DateTime? TargetDate { get; set; }
}

/// <summary>Updates an open investigation's working notes.</summary>
public class UpdateGrievanceInvestigationDto
{
    [MaxLength(4000)]
    public string? Findings { get; set; }

    [MaxLength(4000)]
    public string? EvidenceCollected { get; set; }

    [MaxLength(4000)]
    public string? Recommendation { get; set; }

    public DateTime? TargetDate { get; set; }
}

/// <summary>
/// Concludes an investigation. Findings are required here and nowhere else: an investigation
/// reported as complete with nothing in it is worse than one still open, because the rungs above
/// will rely on it.
/// </summary>
public class CompleteGrievanceInvestigationDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(20)]
    public string Findings { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? EvidenceCollected { get; set; }

    [MaxLength(4000)]
    public string? Recommendation { get; set; }
}

/// <summary>
/// Resolves the case by recording what was decided — FR-HR-181 obligation 8.
/// </summary>
/// <remarks>
/// This is the act that <c>respond(resolvesGrievance: true)</c> used to stand in for. That path
/// still works and still resolves, but it produces an outcome of
/// <see cref="GrievanceResolutionOutcome.NotRecorded"/>; this one requires a real outcome, and may
/// also be used once on an already-resolved case to fill that gap in.
/// </remarks>
public class ResolveGrievanceDto
{
    [Required]
    public GrievanceResolutionOutcome Outcome { get; set; }

    [Required]
    [MaxLength(4000)]
    [MinLength(20)]
    public string Decision { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? RemedyOrUndertakings { get; set; }
}

/// <summary>
/// Closes a case that has exhausted the ladder without being resolved — the first writer
/// <see cref="GrievanceStatus.Closed"/> has ever had.
/// </summary>
public class CloseGrievanceDto
{
    [Required]
    [MaxLength(1000)]
    [MinLength(10)]
    public string Reason { get; set; } = string.Empty;
}

// ── Area 9c slice 4 — conferencing, mediation and union consultation ────────

/// <summary>
/// Convenes a meeting on the case. HR's act.
/// </summary>
/// <remarks>
/// Exactly one of the chair and the external chair name is supplied — a mediator is very often
/// neither party's colleague. <c>UnionId</c> is required for a union consultation and refused for
/// anything else.
/// </remarks>
public class ScheduleGrievanceConferenceDto
{
    [Required]
    public GrievanceConferenceType ConferenceType { get; set; }

    [Required]
    public DateTime ScheduledFor { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    public Guid? ChairId { get; set; }

    [MaxLength(200)]
    public string? ExternalChairName { get; set; }

    [MaxLength(200)]
    public string? ExternalChairOrganisation { get; set; }

    /// <summary>Required for a union consultation — FR-HR-181 obligation 6.</summary>
    public Guid? UnionId { get; set; }

    [MaxLength(1000)]
    public string? Purpose { get; set; }
}

/// <summary>Amends a meeting that has not happened yet. A null field means "leave alone".</summary>
public class UpdateGrievanceConferenceDto
{
    public DateTime? ScheduledFor { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    public Guid? ChairId { get; set; }

    [MaxLength(200)]
    public string? ExternalChairName { get; set; }

    [MaxLength(1000)]
    public string? Purpose { get; set; }
}

/// <summary>
/// Records that the meeting happened. An outcome is required here and nowhere else: a meeting
/// recorded as held with nothing in it tells the rungs above a step was taken that nobody can read.
/// </summary>
public class HoldGrievanceConferenceDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(20)]
    public string Outcome { get; set; } = string.Empty;

    /// <summary>⚠ Redacted to HR and the chair on read. FR-HR-181's "union consultation notes".</summary>
    [MaxLength(6000)]
    public string? Notes { get; set; }

    /// <summary>Who actually came. Any attendee omitted here keeps whatever was already recorded.</summary>
    public List<ConferenceAttendanceDto> Attendance { get; set; } = new();
}

/// <summary>One attendee's attendance, recorded when the meeting is held.</summary>
public class ConferenceAttendanceDto
{
    [Required]
    public Guid AttendeeId { get; set; }

    public bool DidAttend { get; set; }

    [MaxLength(500)]
    public string? ApologyReason { get; set; }
}

public class CancelGrievanceConferenceDto
{
    [Required]
    [MaxLength(500)]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Adds somebody to a meeting. Internal or external — exactly one.</summary>
public class AddConferenceAttendeeDto
{
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(200)]
    public string? ExternalOrganisation { get; set; }

    [MaxLength(200)]
    public string? Capacity { get; set; }
}

// ── Area 9c slice 3 — documents and the signed agreement ─────────────────────

/// <summary>
/// The employee confirms FR-HR-181's final signed agreement.
/// </summary>
/// <remarks>
/// ⚠ Their own act, like escalating and withdrawing, and refused to HR — which is also why this is
/// an acceptance rather than a workflow approval (decision D-10). If they decline, nothing routes
/// anywhere: the case is simply not settled and their remedy is the ladder they already have.
/// </remarks>
public class AcceptGrievanceAgreementDto
{
    /// <summary>Optional note from the employee on accepting. Not a condition of acceptance.</summary>
    [MaxLength(1000)]
    public string? Comment { get; set; }
}

#endregion

// ============================================================================
// DISCIPLINE REMINDER ENGINE DTOs — area 9 slice 8
// ============================================================================

#region Discipline Reminders

public class DisciplineReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
}

public class DisciplineReminderRunResultDto
{
    public Guid RunId { get; set; }
    public int RemindersQueued { get; set; }

    /// <summary>Counts per reminder kind, so a run-now shows what it actually found.</summary>
    public Dictionary<string, int> ByKind { get; set; } = new();
}

/// <summary>
/// One reminder a sweep WOULD fire. Same fields the log records, plus the dedupe key, so a preview
/// can be checked against what the run afterwards actually claimed.
/// </summary>
public class DisciplineReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public string DedupeKey { get; set; } = string.Empty;
}

public class DisciplineReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime DispatchedAt { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE-RELATIONS RESPONDER MATRIX — area 9c slice 5, FR-HR-084
// ============================================================================

#region Responder matrix

public class EmployeeRelationsResponderDto
{
    public Guid Id { get; set; }

    /// <summary>Null is the tenant-wide default, not a missing value.</summary>
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    /// <summary>What the admin screen shows in the unit column for a default row.</summary>
    public string ScopeName => OrganizationUnitName ?? "All units (default)";

    public GrievanceEscalationLevel Level { get; set; }
    public string LevelName => Level.ToString();

    public Guid ResponderEmployeeId { get; set; }
    public string? ResponderName { get; set; }
    public string? ResponderEmployeeNumber { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Notes { get; set; }

    /// <summary>True when this row is the one in force today.</summary>
    public bool IsCurrent { get; set; }
}

public class UpsertEmployeeRelationsResponderDto
{
    /// <summary>Omit for the tenant-wide default.</summary>
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public GrievanceEscalationLevel Level { get; set; }

    [Required]
    public Guid ResponderEmployeeId { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// What the matrix would resolve to, without filing anything — so the admin screen can show the
/// answer and, just as importantly, show where it resolves to nobody.
/// </summary>
public class ResponderResolutionDto
{
    public Guid? OrganizationUnitId { get; set; }
    public GrievanceEscalationLevel Level { get; set; }
    public string LevelName => Level.ToString();

    public Guid? ResponderEmployeeId { get; set; }
    public string? ResponderName { get; set; }

    /// <summary>"Unit", "Default", or "None" — which rule produced this answer.</summary>
    public string ResolvedBy { get; set; } = "None";

    /// <summary>
    /// False when nothing matched. ⚠ A supported outcome, not an error: the case is still filed and
    /// HR routes it by hand, exactly as every case did before the matrix existed.
    /// </summary>
    public bool Resolved => ResponderEmployeeId != null;
}

/// <summary>Every rung's answer for one unit — the row the admin screen renders.</summary>
public class ResponderCoverageDto
{
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public List<ResponderResolutionDto> Levels { get; set; } = new();

    /// <summary>How many of the six rungs resolve to somebody. The number HR is trying to raise.</summary>
    public int CoveredLevels => Levels.Count(l => l.Resolved);
}

#endregion

// ============================================================================
// ANONYMOUS / WHISTLEBLOWER INTAKE — area 9c slice 6, decisions D-2 and D-9
// ============================================================================

#region Anonymous concerns

/// <summary>One message on a concern's thread.</summary>
public class ConcernUpdateDto
{
    public Guid Id { get; set; }
    public Guid ConcernId { get; set; }

    /// <summary>True for the reporter's messages, which carry no author by design.</summary>
    public bool IsFromReporter { get; set; }

    /// <summary>⚠ HR's messages only. Null on a reporter's message, always.</summary>
    public string? AuthorName { get; set; }

    public string Body { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}

/// <summary>
/// A concern reported without a name. ⚠ Carries nothing that identifies the reporter, on any path.
/// </summary>
public class EmployeeRelationsConcernDto
{
    public Guid Id { get; set; }
    public string ConcernNumber { get; set; } = string.Empty;

    public ConcernCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public string Subject { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;

    public ConcernStatus Status { get; set; }
    public ConcernStatus StatusValue => Status;
    public string StatusName => Status.ToString();

    public DateTime ReportedAt { get; set; }

    // HR's side — attributed, because only the reporter is anonymous.
    public string? TriageNotes { get; set; }
    public DateTime? TriagedAt { get; set; }
    public string? TriagedByName { get; set; }

    public DateTime? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public string? ClosedByName { get; set; }

    public Guid? ConvertedCaseId { get; set; }
    public string? ConvertedCaseNumber { get; set; }

    public List<ConcernUpdateDto> Updates { get; set; } = new();
}

/// <summary>
/// The one and only response that carries the retrieval code.
/// </summary>
/// <remarks>
/// ⚠ Shown once and never again: only a PBKDF2 hash is stored, so nobody — HR included — can look
/// it up or reissue it. A reporter who loses it has lost their thread, and that is the correct
/// trade: a recoverable code would have to be recoverable BY somebody, and that somebody could then
/// read the thread.
/// </remarks>
public class ConcernReceiptDto
{
    public string ConcernNumber { get; set; } = string.Empty;

    /// <summary>⚠ Returned exactly once, by the report endpoint. Never stored, never re-sent.</summary>
    public string RetrievalCode { get; set; } = string.Empty;

    public DateTime ReportedAt { get; set; }

    public string Notice { get; set; } =
        "Keep this code safe. It is the only way to follow up on this report, and it cannot be "
        + "recovered or reissued — not even by HR.";
}

public class ReportConcernDto
{
    [Required]
    public ConcernCategory Category { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(6000)]
    [MinLength(20)]
    public string Statement { get; set; } = string.Empty;
}

/// <summary>How a nameless reporter comes back to their own report.</summary>
public class TrackConcernDto
{
    [Required]
    [MaxLength(50)]
    public string ConcernNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RetrievalCode { get; set; } = string.Empty;
}

/// <summary>The reporter adds to their own thread, still without giving a name.</summary>
public class AddConcernUpdateDto : TrackConcernDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(5)]
    public string Body { get; set; } = string.Empty;
}

/// <summary>HR's reply on the thread. Attributed — the desk is accountable.</summary>
public class ReplyToConcernDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(5)]
    public string Body { get; set; } = string.Empty;
}

public class TriageConcernDto
{
    [Required]
    [MaxLength(4000)]
    [MinLength(10)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Where triage leaves it — under review, or closed outright.</summary>
    [Required]
    public ConcernStatus Status { get; set; }
}

public class CloseConcernDto
{
    [Required]
    [MaxLength(1000)]
    [MinLength(10)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Converts a concern into a named employee-relations case.
/// </summary>
/// <remarks>
/// ⚠ <see cref="CaseType"/> may not be <c>Grievance</c>. A grievance is the employee's own act and
/// HR cannot raise one for anybody — converting a concern into one would be the raise-on-behalf-of
/// that <c>OpenCaseAsync</c> refuses, reached by a longer route.
/// </remarks>
public class ConvertConcernDto
{
    [Required]
    public EmployeeRelationsCaseType CaseType { get; set; }

    /// <summary>Whom the case is about — the reporter if they have identified themselves, or the
    /// employee the concern names.</summary>
    [Required]
    public Guid EmployeeId { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE-RELATIONS ANALYTICS — area 9c slice 8
// ============================================================================

#region Analytics

/// <summary>
/// One count in a breakdown, with the total it is a part of.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Total"/> travels WITH every slice, and that is the point. A count of 12 means
/// nothing on its own — 12 of 15 is a crisis and 12 of 4,000 is background noise — and a screen
/// that has to fetch the denominator separately will eventually render one without it.
/// </remarks>
public class ErCountSliceDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>The denominator this count is a part of. Never omitted.</summary>
    public int Total { get; set; }

    /// <summary>
    /// The share, or <c>null</c> when there is nothing to take a share OF.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Null, not zero.</b> 0% means "none of them"; a total of zero means "there were none",
    /// which is a different fact and must not be rendered as compliance. The area-7 lesson: a
    /// compliance rate shipped without its denominator read 0% whether nobody complied or nobody
    /// was asked.
    /// </remarks>
    public decimal? Percent => Total == 0 ? null : Math.Round(Count * 100m / Total, 1);
}

/// <summary>
/// A rate, carried with both the numbers that produced it.
/// </summary>
/// <remarks>
/// ⚠ There is deliberately no constructor that takes only a percentage. A rate whose numerator and
/// denominator are not on the same object is a rate somebody will eventually render alone.
/// </remarks>
public class ErRateDto
{
    public string Label { get; set; } = string.Empty;
    public int Numerator { get; set; }
    public int Denominator { get; set; }

    /// <summary>⚠ Null when the denominator is zero. See <see cref="ErCountSliceDto.Percent"/>.</summary>
    public decimal? Percent => Denominator == 0 ? null : Math.Round(Numerator * 100m / Denominator, 1);

    /// <summary>True when there was nothing to measure. The screen shows "no data", not "0%".</summary>
    public bool NoData => Denominator == 0;
}

/// <summary>How long cases take to settle. Every figure null when nothing has been resolved.</summary>
public class ErResolutionTimeDto
{
    /// <summary>
    /// Cases counted here — resolved only.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Withdrawn cases are excluded, and that is a judgement worth knowing.</b> A withdrawal is
    /// not a resolution: including them would shorten the average every time somebody gave up, so
    /// the number would improve as the process got worse. They stay in <c>TotalCases</c>.
    /// </remarks>
    public int ResolvedCount { get; set; }

    public decimal? AverageDays { get; set; }

    /// <summary>Reported beside the mean because one case stuck for a year drags the mean and not this.</summary>
    public decimal? MedianDays { get; set; }

    public int? LongestDays { get; set; }
}

/// <summary>A rung with cases sitting on it unanswered — HR's operational view.</summary>
public class ErStuckRungDto
{
    public GrievanceEscalationLevel Level { get; set; }
    public string LevelName => Level.ToString();
    public int Count { get; set; }

    /// <summary>How long the oldest has been waiting. The number that makes this list actionable.</summary>
    public int? OldestWaitingDays { get; set; }

    /// <summary>How many of them have nobody named to answer — the responder-matrix gap, in cases.</summary>
    public int Unassigned { get; set; }
}

/// <summary>
/// Employee-relations analytics — area 9c slice 8.
/// </summary>
/// <remarks>
/// ⚠ Every breakdown carries its own denominator and every rate carries both its numbers. The
/// area-7 lesson that produced this shape: a dashboard and its analytics page disagreed about the
/// same figure because each counted a slightly different set, and a compliance rate shipped without
/// a denominator read 0% whether nobody complied or nobody was asked.
/// </remarks>
public class EmployeeRelationsAnalyticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>
    /// Every case in the window. <b>The denominator for the whole page</b> — all breakdowns below
    /// are subsets of this, so a screen can never mix two different totals.
    /// </summary>
    public int TotalCases { get; set; }

    public int OpenCases { get; set; }
    public int ResolvedCases { get; set; }
    public int WithdrawnCases { get; set; }
    public int ClosedUnresolvedCases { get; set; }

    public List<ErCountSliceDto> ByCaseType { get; set; } = new();
    public List<ErCountSliceDto> ByStatus { get; set; } = new();

    /// <summary>Where OPEN cases currently sit. Denominator is <see cref="OpenCases"/>, not the total.</summary>
    public List<ErCountSliceDto> ByCurrentLevel { get; set; } = new();

    /// <summary>What RESOLVED cases decided. Denominator is <see cref="ResolvedCases"/>.</summary>
    public List<ErCountSliceDto> ByOutcome { get; set; } = new();

    /// <summary>Cases per organisation unit, busiest first.</summary>
    public List<ErCountSliceDto> ByOrganizationUnit { get; set; } = new();

    /// <summary>
    /// Cases that were escalated at least once, over cases filed.
    /// </summary>
    /// <remarks>
    /// The management figure rather than the operational one: a unit whose cases nearly all escalate
    /// has a problem at the rung below, not a grievance problem.
    /// </remarks>
    public ErRateDto EscalationRate { get; set; } = new();

    /// <summary>
    /// Resolved cases whose outcome was never captured, over resolved cases.
    /// </summary>
    /// <remarks>
    /// ⚠ This is slice 2's <c>NotRecorded</c> made visible and countable. It exists so the honest
    /// gap can be worked off rather than forgotten — a number HR can drive to zero.
    /// </remarks>
    public ErRateDto OutcomeNotRecordedRate { get; set; } = new();

    /// <summary>
    /// Settlements with no signed agreement on file, over settlements.
    /// </summary>
    /// <remarks>FR-HR-181 obligation 9, measured. See also the slice-7 sweep that chases it.</remarks>
    public ErRateDto AgreementMissingRate { get; set; } = new();

    public ErResolutionTimeDto ResolutionTime { get; set; } = new();

    public List<ErStuckRungDto> StuckAtRung { get; set; } = new();

    // ── Anonymous intake (area 9c slice 6) ───────────────────────────────────

    /// <summary>
    /// Concerns reported in the window.
    /// </summary>
    /// <remarks>
    /// ⚠ Counts only. Nothing here is broken down by anything that could narrow who reported —
    /// no unit, no reporter, no cross-tab. A concern count by unit in a small unit is an
    /// identification, not a statistic.
    /// </remarks>
    public int ConcernsReported { get; set; }
    public int ConcernsUntriaged { get; set; }
    public int ConcernsConverted { get; set; }
    public List<ErCountSliceDto> ConcernsByCategory { get; set; } = new();
}

#endregion
