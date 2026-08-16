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

public class StaffGrievanceSummaryDto
{
    public Guid Id { get; set; }
    public string GrievanceNumber { get; set; } = string.Empty;
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

#endregion
