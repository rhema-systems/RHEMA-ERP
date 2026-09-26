using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Teams and committees, slice F2 — meetings, decisions, reviews and the dashboard (plan § 6.6).
// ═══════════════════════════════════════════════════════════════════════════════════════════════

#region Meetings

public class TeamMeetingListDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public TeamMeetingKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public DateTime? HeldAt { get; set; }
    public string? Venue { get; set; }
    public TeamMeetingStatus Status { get; set; }
    public Guid? ChairMemberId { get; set; }
    public string? ChairName { get; set; }

    /// <summary>Whether the signed minutes are on the row. The download affordance keys off this.</summary>
    public bool HasMinutesDocument { get; set; }
    public string? MinutesFileName { get; set; }

    public int AttendeeCount { get; set; }

    /// <summary>
    /// How many of the invitees actually came.
    /// </summary>
    /// <remarks>
    /// ⚠ Counts only rows where attendance is KNOWN to be true. Before a meeting is held every
    /// attendance is null, so this is 0 — which reads correctly as "nobody has come yet", not as
    /// "nobody came".
    /// </remarks>
    public int AttendedCount { get; set; }

    public int DecisionCount { get; set; }

    /// <summary>Decisions that have been turned into a task. The rest are still only minutes.</summary>
    public int DecisionsWithTaskCount { get; set; }
}

public class TeamMeetingDetailDto : TeamMeetingListDto
{
    public string? Agenda { get; set; }
    public string? Minutes { get; set; }
    public string? CancelledReason { get; set; }
    public string? MinutesMimeType { get; set; }
    public long? MinutesFileSizeBytes { get; set; }

    public List<TeamMeetingAttendeeDto> Attendees { get; set; } = new();
    public List<TeamMeetingDecisionDto> Decisions { get; set; } = new();
}

public class CreateTeamMeetingDto
{
    public TeamMeetingKind Kind { get; set; } = TeamMeetingKind.Meeting;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public DateTime ScheduledAt { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    [MaxLength(4000)]
    public string? Agenda { get; set; }

    /// <summary>Must be an active member of this team.</summary>
    public Guid? ChairMemberId { get; set; }

    /// <summary>
    /// Who is expected. Members of this team only.
    /// </summary>
    /// <remarks>
    /// ⚠ A REPLACE SET on the update: the list sent IS the invitee list, and an invitee omitted is
    /// an invitee removed. Same convention as the other parent-carries-its-children payloads in HR
    /// — see the replace-set note in the plan — and the reason the update DTO says so again.
    /// </remarks>
    public List<Guid> AttendeeMemberIds { get; set; } = new();
}

/// <remarks>
/// ⚠ <c>AttendeeMemberIds</c> REPLACES the invitee list. Omitting somebody removes them; sending an
/// empty list removes everybody. Attendance already recorded against a member who stays on the list
/// is preserved — only the membership of the list is replaced.
/// </remarks>
public class UpdateTeamMeetingDto : CreateTeamMeetingDto
{
}

/// <summary>Body for holding a meeting — the minutes and who actually came.</summary>
public class HoldTeamMeetingDto
{
    /// <summary>When it actually happened. Defaults to now.</summary>
    public DateTime? HeldAt { get; set; }

    [MaxLength(8000)]
    public string? Minutes { get; set; }

    /// <summary>
    /// Attendance, per invitee. Anyone omitted keeps whatever was already recorded.
    /// </summary>
    /// <remarks>
    /// ⚠ NOT a replace set, unlike the invitee list. Marking the register is done in passes — the
    /// chair ticks who is in the room, then adds an apology that arrives late — and replacing the
    /// whole set on each save would wipe the earlier pass.
    /// </remarks>
    public List<TeamMeetingAttendanceDto> Attendance { get; set; } = new();
}

public class TeamMeetingAttendanceDto
{
    [Required]
    public Guid MemberId { get; set; }

    public bool? Attended { get; set; }

    public bool Apology { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class TeamMeetingAttendeeDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Guid MemberId { get; set; }
    public string? MemberName { get; set; }
    public Guid? EmployeeId { get; set; }

    /// <summary>⚠ Null means "not yet known", not "absent". See the entity.</summary>
    public bool? Attended { get; set; }

    public bool Apology { get; set; }
    public string? Notes { get; set; }
}

public class TeamMeetingDecisionDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public int DisplayOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid? ResponsibleMemberId { get; set; }
    public string? ResponsibleName { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>The task raised from this decision, if one has been.</summary>
    public Guid? RaisedTaskId { get; set; }

    /// <summary>The raised task's title, so the minute book can show what became of the decision.</summary>
    public string? RaisedTaskTitle { get; set; }
}

public class CreateTeamMeetingDecisionDto
{
    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public Guid? ResponsibleMemberId { get; set; }

    public DateOnly? DueDate { get; set; }
}

public class UpdateTeamMeetingDecisionDto : CreateTeamMeetingDecisionDto
{
}

/// <summary>
/// Turns a decision into a task.
/// </summary>
/// <remarks>
/// ⚠ The title and the objective are the only things the caller supplies. The assignee and the due
/// date come from the DECISION — that is the whole point: an action item that quietly acquired a
/// different owner from the one the meeting named would be worse than no link at all.
/// </remarks>
public class RaiseTaskFromDecisionDto
{
    /// <summary>Defaults to the decision's own text, trimmed to fit a task title.</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Optional: put the raised task under an objective.</summary>
    public Guid? ObjectiveId { get; set; }

    public TeamTaskPriority Priority { get; set; } = TeamTaskPriority.Normal;
}

#endregion

#region Reviews

public class TeamReviewListDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string? ReviewedByName { get; set; }
    public int? OverallRating { get; set; }
    public TeamReviewStatus Status { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedOn { get; set; }
    public int LineCount { get; set; }
}

public class TeamReviewDetailDto : TeamReviewListDto
{
    public string? Summary { get; set; }
    public string? Recommendations { get; set; }
    public string? AcknowledgementNote { get; set; }
    public List<TeamReviewLineDto> Lines { get; set; } = new();
}

public class CreateTeamReviewDto
{
    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    [Range(1, 5)]
    public int? OverallRating { get; set; }

    [MaxLength(4000)]
    public string? Summary { get; set; }

    [MaxLength(4000)]
    public string? Recommendations { get; set; }

    // ⚠ No ReviewedById. The reviewer is the token's employee, never a caller-supplied id — the
    // actor-from-token convention this module has had to repair in three other areas.
}

public class UpdateTeamReviewDto : CreateTeamReviewDto
{
}

public class TeamReviewLineDto
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid ObjectiveId { get; set; }
    public string? ObjectiveTitle { get; set; }

    /// <summary>⚠ What the objective stood at WHEN THE LINE WAS WRITTEN, not now. See the entity.</summary>
    public int ProgressAtReview { get; set; }

    public int? Rating { get; set; }
    public string? Comment { get; set; }
}

public class UpsertTeamReviewLineDto
{
    [Required]
    public Guid ObjectiveId { get; set; }

    [Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    // ⚠ No ProgressAtReview. It is snapshotted from the objective by the service; letting a caller
    // supply it would let a review claim a figure the objective never held.
}

public class AcknowledgeTeamReviewDto
{
    [MaxLength(2000)]
    public string? Note { get; set; }
}

#endregion

#region Dashboard

/// <summary>
/// One screen's worth of "how is this team doing".
/// </summary>
/// <remarks>
/// ⚠ Assembled server-side in one read rather than by the screen firing six. The tiles have to
/// agree with each other — an overdue count that disagrees with the list below it is worse than no
/// tile — and they can only be guaranteed to if one query answers them all at one instant.
/// </remarks>
public class TeamDashboardDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    // ── The charter ──────────────────────────────────────────────────────────
    public Guid? TermsOfReferenceId { get; set; }
    public int? TermsVersion { get; set; }
    public TeamTorStatus? TermsStatus { get; set; }

    /// <summary>Null when open-ended or when there are no approved terms at all.</summary>
    public int? TermsDaysUntilExpiry { get; set; }

    /// <summary>⚠ True when the team has NO approved terms — a committee operating without a charter.</summary>
    public bool HasNoApprovedTerms { get; set; }

    // ── Objectives ───────────────────────────────────────────────────────────
    public int ObjectivesActive { get; set; }
    public int ObjectivesCompleted { get; set; }
    public int ObjectivesOverdue { get; set; }

    /// <summary>Mean progress across the ACTIVE objectives. Zero when there are none.</summary>
    public int AverageProgressPercent { get; set; }

    public int ObjectiveWeightTotal { get; set; }
    public bool ObjectiveWeightsBalanced { get; set; }

    // ── Tasks ────────────────────────────────────────────────────────────────
    public int TasksOpen { get; set; }
    public int TasksOverdue { get; set; }
    public int TasksDueThisWeek { get; set; }
    public int TasksBlocked { get; set; }

    /// <summary>⚠ Open tasks nobody owns. The count a lead most needs and least often has.</summary>
    public int TasksUnassigned { get; set; }

    // ── Meetings ─────────────────────────────────────────────────────────────
    public Guid? NextMeetingId { get; set; }
    public string? NextMeetingTitle { get; set; }
    public DateTime? NextMeetingAt { get; set; }
    public DateTime? LastMeetingHeldAt { get; set; }

    /// <summary>Decisions with a responsible member that have not become a task.</summary>
    public int UnactionedDecisions { get; set; }

    // ── Reviews ──────────────────────────────────────────────────────────────
    public DateOnly? LastReviewPeriodEnd { get; set; }
    public int? LastReviewRating { get; set; }
    public TeamReviewStatus? LastReviewStatus { get; set; }
}

#endregion

#region The sweep

public class TeamReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
}

public class TeamReminderDispatchDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid TeamId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public Guid? RoutedToEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TeamReminderRunResultDto
{
    public Guid RunId { get; set; }
    public int RemindersQueued { get; set; }
    public int TeamsSwept { get; set; }

    /// <summary>Per-kind counts, so a run-now can say what it actually found.</summary>
    public Dictionary<string, int> ByKind { get; set; } = new();
}

#endregion
