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
