using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Teams and committees — terms of reference, objectives and tasks (round 2, lane F1; plan § 6.6).
//
//  ⚠ Read DTOs carry the RESOLVED member and employee names. A tasks board that renders a row per
//  task and then fetches a name per row is the N+1 every list projection in this module has had to
//  be repaired for; the services Include what they project.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

#region Terms of reference

public class TeamTermsOfReferenceListDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public int Version { get; set; }
    public TeamTorStatus Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }

    /// <summary>Whether the signed charter is on the row. The download affordance keys off this.</summary>
    public bool HasDocument { get; set; }
    public string? DocumentFileName { get; set; }

    /// <summary>
    /// Days until <see cref="EffectiveTo"/>, negative once past. Null when open-ended.
    /// </summary>
    /// <remarks>
    /// ⚠ Computed server-side because the nightly sweep and the dashboard must agree on what
    /// "expiring" means. Two definitions of the same countdown is how a badge and an email come to
    /// disagree about the same charter.
    /// </remarks>
    public int? DaysUntilExpiry { get; set; }
}

public class TeamTermsOfReferenceDetailDto : TeamTermsOfReferenceListDto
{
    public Guid? PreviousVersionId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? Authority { get; set; }
    public string? MembershipRules { get; set; }
    public string? MeetingCadence { get; set; }
    public string? ReportingLine { get; set; }
    public string? Deliverables { get; set; }
    public string? Notes { get; set; }
    public string? DocumentMimeType { get; set; }
    public long? DocumentFileSizeBytes { get; set; }
}

public class CreateTeamTermsOfReferenceDto
{
    [Required]
    [MaxLength(4000)]
    public string Purpose { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Scope { get; set; }

    [MaxLength(4000)]
    public string? Authority { get; set; }

    [MaxLength(4000)]
    public string? MembershipRules { get; set; }

    [MaxLength(500)]
    public string? MeetingCadence { get; set; }

    [MaxLength(500)]
    public string? ReportingLine { get; set; }

    [MaxLength(4000)]
    public string? Deliverables { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // ⚠ No document fields. The signed charter arrives through the upload gate, not on a JSON body.
}

/// <summary>
/// Edits a DRAFT terms of reference. An approved one is immutable — take a new version instead.
/// </summary>
public class UpdateTeamTermsOfReferenceDto : CreateTeamTermsOfReferenceDto
{
}

#endregion

#region Objectives

public class TeamObjectiveListDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string? Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public TeamObjectiveStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public TeamObjectiveProgressMode ProgressMode { get; set; }
    public int? Weight { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? OwnerMemberId { get; set; }
    public string? OwnerName { get; set; }

    /// <summary>Tasks under this objective, and how many are done — what the progress is counted from.</summary>
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }

    /// <summary>Past its due date and not finished. Server-computed, for the same reason as the ToR countdown.</summary>
    public bool IsOverdue { get; set; }
}

public class TeamObjectiveDetailDto : TeamObjectiveListDto
{
    public string? Description { get; set; }
    public string? Measure { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? OutcomeSummary { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public string? CancelledReason { get; set; }
}

public class CreateTeamObjectiveDto
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? Measure { get; set; }

    public decimal? TargetValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Range(0, 100)]
    public int? Weight { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Must be an ACTIVE member of this team — the service refuses anyone else.</summary>
    public Guid? OwnerMemberId { get; set; }

    public TeamObjectiveProgressMode ProgressMode { get; set; } = TeamObjectiveProgressMode.FromTasks;

    /// <summary>
    /// ⚠ Only honoured when <see cref="ProgressMode"/> is <c>Manual</c>. Under <c>FromTasks</c> the
    /// service computes it from the tasks and a supplied value is REFUSED, not ignored — a field
    /// that accepts input and silently discards it is the defect lane D1 removed from the probation
    /// form.
    /// </summary>
    [Range(0, 100)]
    public int? ProgressPercent { get; set; }
}

public class UpdateTeamObjectiveDto : CreateTeamObjectiveDto
{
}

/// <summary>Body for the status transitions that need to say something.</summary>
public class TeamObjectiveStatusChangeDto
{
    /// <summary>
    /// How the team achieved it. Required to complete an objective below 100 % — the feedback asked
    /// for "whether they have done it, and how they did it".
    /// </summary>
    [MaxLength(4000)]
    public string? OutcomeSummary { get; set; }

    /// <summary>Required to cancel. An objective dropped without a reason is a question nobody can answer later.</summary>
    [MaxLength(1000)]
    public string? CancelledReason { get; set; }
}

#endregion

#region Tasks

public class TeamTaskListDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid? ObjectiveId { get; set; }
    public string? ObjectiveTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public TeamTaskStatus Status { get; set; }
    public TeamTaskPriority Priority { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? AssigneeMemberId { get; set; }
    public string? AssigneeName { get; set; }

    /// <summary>The assignee's EMPLOYEE id, so a screen can tell whether the task is the viewer's own.</summary>
    public Guid? AssigneeEmployeeId { get; set; }

    public string? BlockedReason { get; set; }
    public bool IsOverdue { get; set; }

    public int ChecklistTotal { get; set; }
    public int ChecklistDone { get; set; }
    public int AttachmentCount { get; set; }
}

public class TeamTaskDetailDto : TeamTaskListDto
{
    public string? Description { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public string? CompletionNotes { get; set; }

    /// <summary>The meeting decision this came out of, where it did. Provenance only until slice F2.</summary>
    public Guid? SourceMeetingDecisionId { get; set; }

    public List<TeamTaskChecklistItemDto> ChecklistItems { get; set; } = new();
    public List<TeamTaskAttachmentDto> Attachments { get; set; } = new();
}

public class CreateTeamTaskDto
{
    public Guid? ObjectiveId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>Must be an ACTIVE member of this team — the service refuses anyone else.</summary>
    public Guid? AssigneeMemberId { get; set; }

    public TeamTaskPriority Priority { get; set; } = TeamTaskPriority.Normal;

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }
}

public class UpdateTeamTaskDto : CreateTeamTaskDto
{
}

/// <summary>
/// Moves a task, and carries the words the new status requires.
/// </summary>
/// <remarks>
/// Its own door rather than a field on the update, so a member who may only progress THEIR OWN task
/// can be let through here without being given the whole task record to rewrite.
/// </remarks>
public class TeamTaskStatusChangeDto
{
    [Required]
    public TeamTaskStatus Status { get; set; }

    /// <summary>⚠ Required when moving to <c>Blocked</c>.</summary>
    [MaxLength(1000)]
    public string? BlockedReason { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }
}

public class TeamTaskChecklistItemDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public int DisplayOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public string? DoneByName { get; set; }
    public DateTime? DoneAt { get; set; }
}

public class CreateTeamTaskChecklistItemDto
{
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public class UpdateTeamTaskChecklistItemDto
{
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    /// <summary>
    /// ⚠ Ticking is not editing. Setting this here records who ticked it and when; clearing it
    /// wipes both, because a tick nobody owns is worse than no tick.
    /// </summary>
    public bool IsDone { get; set; }
}

public class TeamTaskAttachmentDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string? Title { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion
