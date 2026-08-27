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
