using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Teams and committees, slice F2 — how the team actually works: what it met about, what it
//  decided, and how it did. Round 2, lane F (plan § 6.6).
//
//  ⚠ The meeting record is a committee's minute book, and the DECISION row is the part that matters
//  most: "Create task" turns a decision into a TeamTask, which is the only mechanism that stops a
//  minute book being a place where actions go to be forgotten.
//
//  Same conventions as TeamActivityEntities.cs — TenantId stamped explicitly, children added
//  through their own repository, unique indexes filtered on IsDeleted, files through the gate.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A gathering of the team — a meeting, a workshop, a site visit.</summary>
/// <remarks>
/// ⚠ <see cref="ScheduledAt"/> and <see cref="HeldAt"/> are DIFFERENT dates and both are kept. A
/// meeting moved twice and finally held is the ordinary case, and a minute book that overwrote the
/// scheduled date could not answer "did this committee meet when its terms of reference said it
/// should" — which is exactly the question an auditor asks of a committee.
/// </remarks>
public class TeamMeeting : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    public TeamMeetingKind Kind { get; set; } = TeamMeetingKind.Meeting;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>When it was called for.</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>When it actually happened. Null until it is held.</summary>
    public DateTime? HeldAt { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    [MaxLength(4000)]
    public string? Agenda { get; set; }

    [MaxLength(8000)]
    public string? Minutes { get; set; }

    public TeamMeetingStatus Status { get; set; } = TeamMeetingStatus.Scheduled;

    /// <summary>Who chaired it. A member of this team.</summary>
    public Guid? ChairMemberId { get; set; }

    [ForeignKey(nameof(ChairMemberId))]
    public virtual TeamMember? ChairMember { get; set; }

    [MaxLength(1000)]
    public string? CancelledReason { get; set; }

    // ── The signed minutes, through the controlled gate ───────────────────────
    // Three ids and the metadata, never a path.
    public Guid? MinutesFileUploadRecordId { get; set; }
    public Guid? MinutesDocumentRecordId { get; set; }
    public Guid? MinutesDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? MinutesFileName { get; set; }

    [MaxLength(150)]
    public string? MinutesMimeType { get; set; }

    public long? MinutesFileSizeBytes { get; set; }

    public virtual ICollection<TeamMeetingAttendee> Attendees { get; set; } = new List<TeamMeetingAttendee>();

    public virtual ICollection<TeamMeetingDecision> Decisions { get; set; } = new List<TeamMeetingDecision>();
}

/// <summary>
/// Who was expected at a meeting, and whether they came.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Attended"/> is <c>bool?</c>, not <c>bool</c>. Three states are real and a boolean
/// holds two: expected-and-came, expected-and-did-not, and <b>not yet known</b> because the meeting
/// has not happened. Defaulting the third to <c>false</c> would mark every invitee absent from the
/// moment the meeting was scheduled, and anything counting attendance would read a meeting nobody
/// had missed yet as one nobody attended.
/// </remarks>
public class TeamMeetingAttendee : TenantEntity
{
    [Required]
    public Guid MeetingId { get; set; }

    [ForeignKey(nameof(MeetingId))]
    public virtual TeamMeeting Meeting { get; set; } = null!;

    [Required]
    public Guid MemberId { get; set; }

    [ForeignKey(nameof(MemberId))]
    public virtual TeamMember Member { get; set; } = null!;

    /// <summary>Null until the meeting is held. See the remark on this class.</summary>
    public bool? Attended { get; set; }

    /// <summary>Whether they sent apologies. Distinct from simply not turning up.</summary>
    public bool Apology { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Something the meeting decided.
/// </summary>
/// <remarks>
/// <para><b>⚠ <see cref="RaisedTaskId"/> is the point of this table.</b> A decision with somebody
/// responsible and a date is an action item, and an action item that lives only in minutes is one
/// nobody chases. "Create task" materialises a <c>TeamTask</c> and links it both ways — the task
/// carries <c>SourceMeetingDecisionId</c> back, which slice F1 added as a bare provenance id
/// precisely so this slice could complete the pair.</para>
///
/// <para>⚠ No navigation and no foreign key on <see cref="RaisedTaskId"/>, deliberately.
/// <c>TeamTask.SourceMeetingDecisionId</c> already points this way, and configuring a second
/// relationship between the same pair of tables is how EF mints a shadow FK column beside one of
/// them — the <c>GeoAreaId1</c> shape.</para>
/// </remarks>
public class TeamMeetingDecision : TenantEntity
{
    [Required]
    public Guid MeetingId { get; set; }

    [ForeignKey(nameof(MeetingId))]
    public virtual TeamMeeting Meeting { get; set; } = null!;

    public int DisplayOrder { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Who is to act on it, where the meeting said.</summary>
    public Guid? ResponsibleMemberId { get; set; }

    [ForeignKey(nameof(ResponsibleMemberId))]
    public virtual TeamMember? ResponsibleMember { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>The task raised from this decision, once one has been. One task per decision.</summary>
    public Guid? RaisedTaskId { get; set; }
}

/// <summary>
/// A periodic look at how the team is doing — a quarter, a project end.
/// </summary>
/// <remarks>
/// ⚠ Deliberately NOT on the workflow engine (plan § 6.6.2). A review is a record of what somebody
/// found, not a request for permission; there is nothing to approve. The lead ACKNOWLEDGES it,
/// which says it was read and nothing about whether they agreed.
/// </remarks>
public class TeamReview : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Who wrote it — the EMPLOYEE from the token, never a caller-supplied id.
    /// </summary>
    /// <remarks>
    /// ⚠ An Employee FK rather than a TeamMember one, because the reviewer is often NOT on the team:
    /// a sponsor, a unit head, HR. That is the normal case for a review, not the exception, and a
    /// TeamMember FK would have made the commonest reviewer unrepresentable.
    /// </remarks>
    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    /// <summary>1 to 5. Null while the review is still a draft.</summary>
    [Range(1, 5)]
    public int? OverallRating { get; set; }

    [MaxLength(4000)]
    public string? Summary { get; set; }

    [MaxLength(4000)]
    public string? Recommendations { get; set; }

    public TeamReviewStatus Status { get; set; } = TeamReviewStatus.Draft;

    public Guid? AcknowledgedById { get; set; }

    [ForeignKey(nameof(AcknowledgedById))]
    public virtual Employee? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedOn { get; set; }

    [MaxLength(2000)]
    public string? AcknowledgementNote { get; set; }

    public virtual ICollection<TeamReviewLine> Lines { get; set; } = new List<TeamReviewLine>();
}

/// <summary>
/// How one objective stood at the moment of a review.
/// </summary>
/// <remarks>
/// ⚠ <see cref="ProgressAtReview"/> is a SNAPSHOT, copied from the objective when the line is
/// written, not a live read. That is the whole reason the column exists: a review says what was
/// true in March, and reading the objective's current figure would silently rewrite every past
/// review every time somebody ticked a task.
/// </remarks>
public class TeamReviewLine : TenantEntity
{
    [Required]
    public Guid ReviewId { get; set; }

    [ForeignKey(nameof(ReviewId))]
    public virtual TeamReview Review { get; set; } = null!;

    [Required]
    public Guid ObjectiveId { get; set; }

    [ForeignKey(nameof(ObjectiveId))]
    public virtual TeamObjective Objective { get; set; } = null!;

    [Range(0, 100)]
    public int ProgressAtReview { get; set; }

    [Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  The nightly sweep's own record.
//
//  ⚠ A sweep that leaves no trace cannot be shown to have run, and TWO HR sweeps in this codebase
//  turned out never to have run at all — the registration was missing and nothing said so. The run
//  row and the dispatch log are what make "did it fire?" answerable from the database rather than
//  from a hopeful reading of the code.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>One execution of the team reminder sweep.</summary>
public class TeamReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<TeamReminderDispatchLog> DispatchLogs { get; set; }
        = new List<TeamReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
/// </summary>
/// <remarks>
/// <para>⚠ The unique <c>(TenantId, DedupeKey)</c> index is the send-once guarantee, not an
/// optimisation: a sweep claims its keys in the same <c>SaveChanges</c> that records the run, so the
/// nightly host and the run-now button cannot double-send even if they overlap. The key encodes the
/// item, the kind and the DUE date — so moving a task's due date re-arms the reminder, which is
/// what a moved deadline should do.</para>
///
/// <para>⚠ Nothing here carries the substance of the work — no description, no blocked reason, no
/// review comment. A reminder travels further than the record it is about, so it names a person, a
/// team and a date, and makes the reader open the record for anything else.</para>
/// </remarks>
public class TeamReminderDispatchLog : TenantEntity
{
    [Required]
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual TeamReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind: "TaskDueSoon", "TaskOverdue", "ObjectiveOverdue", "MeetingTomorrow",
    /// "TermsExpiring".
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item — "Team task", "Team objective", "Team meeting".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The team it belongs to, so a screen can group by team.</summary>
    public Guid TeamId { get; set; }

    /// <summary>What the notification shows: who, which team, and what is due.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// Who it was routed to, when one resolved.
    /// </summary>
    /// <remarks>
    /// ⚠ Null is meaningful, not missing: it records that the sweep found work to chase and had
    /// nobody to chase — an unassigned overdue task, or a team with no lead. That is precisely the
    /// case HR needs surfaced rather than suppressed.
    /// </remarks>
    public Guid? RoutedToEmployeeId { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}
