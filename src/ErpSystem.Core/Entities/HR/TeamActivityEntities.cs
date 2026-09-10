using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Teams and committees — what a team is chartered to do, what it has undertaken, and who is
//  doing it. Round 2, lane F (plan § 1.4, § 6.6).
//
//  The demo feedback asked "do we need a sub-module for committee or team activities?" and the
//  user answered YES, as a full version rather than a lightweight one.
//
//  ⚠ These hang off the EXISTING `Team` record (OrganizationStructureEntities.cs), whose
//  `TeamType` already distinguishes a Committee from a project team or a task force. There is no
//  second "committee" entity and there must not be.
//
//  ⚠ `SafetyCommittee` and `AwardCommittee` stay SEPARATE. They are bounded contexts with their
//  own rules — meeting quorum, nomination scoring — and folding them in here would break two
//  closed areas to save a table.
//
//  Conventions this file follows, each of which was learnt the hard way elsewhere in HR:
//   · Every entity is a TenantEntity and the service stamps TenantId EXPLICITLY — the context's
//     auto-stamp is inert, and an unstamped row inserts TenantId = Guid.Empty and trips the FK.
//   · A child row is added through its OWN repository, never by adding to a tracked parent's
//     navigation collection — that turns the add into an UPDATE of the parent (the SHE
//     checklist-builder lesson).
//   · Unique indexes carry a filter on IsDeleted, so a soft-deleted row does not hold a slot.
//   · Files arrive through the controlled upload gate — three ids and the metadata, never a path.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What a team or committee is chartered to do — its purpose, scope, authority and reporting line.
/// </summary>
/// <remarks>
/// <para><b>Versioned, never edited once approved.</b> Terms of reference are the document a
/// committee's authority rests on; rewriting them in place would silently change what the committee
/// was chartered to do last year. "New version" clones the row to <c>Draft</c> at
/// <see cref="Version"/> + 1 with <see cref="PreviousVersionId"/> pointing back, and approving the
/// new one supersedes the old — the same idiom as the SHE inspection checklist.</para>
///
/// <para><b>⚠ One approved version per team at a time.</b> Enforced by the service, not by an index:
/// the rule is "at most one row for this team in <c>Approved</c>", which a filtered unique index
/// cannot express across a status enum without also forbidding two drafts.</para>
/// </remarks>
public class TeamTermsOfReference : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    /// <summary>1 for the first terms; each new version is the previous plus one.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The row this version was cloned from. Null on the first.</summary>
    public Guid? PreviousVersionId { get; set; }

    [ForeignKey(nameof(PreviousVersionId))]
    public virtual TeamTermsOfReference? PreviousVersion { get; set; }

    public TeamTorStatus Status { get; set; } = TeamTorStatus.Draft;

    /// <summary>Why the team exists at all.</summary>
    [Required]
    [MaxLength(4000)]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>What is in scope — and, as often matters more, what is not.</summary>
    [MaxLength(4000)]
    public string? Scope { get; set; }

    /// <summary>
    /// What the team may decide on its own, and what it may only recommend.
    /// </summary>
    /// <remarks>
    /// ⚠ Free text on purpose. A committee's authority is a sentence from a board resolution, not
    /// an enum; modelling it as one would force every tenant's governance into ours.
    /// </remarks>
    [MaxLength(4000)]
    public string? Authority { get; set; }

    /// <summary>How members are appointed, for how long, and what quorum means here.</summary>
    [MaxLength(4000)]
    public string? MembershipRules { get; set; }

    /// <summary>"Monthly, first Tuesday" — words, because that is how a charter says it.</summary>
    [MaxLength(500)]
    public string? MeetingCadence { get; set; }

    /// <summary>Who the team reports to.</summary>
    [MaxLength(500)]
    public string? ReportingLine { get; set; }

    /// <summary>What the team is expected to produce.</summary>
    [MaxLength(4000)]
    public string? Deliverables { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// When the charter lapses. Null means open-ended.
    /// </summary>
    /// <remarks>
    /// The nightly sweep warns the lead and HR when this is within thirty days — a committee whose
    /// terms have quietly expired is the failure this column exists to prevent.
    /// </remarks>
    public DateOnly? EffectiveTo { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    // ── The signed charter, through the controlled gate ───────────────────────
    // ⚠ Three ids and the metadata, never a path. A caller-supplied file location on a JSON body is
    // the sink that had to be removed from the guarantor form, the award attachment and three
    // medical documents.
    public Guid? DocumentFileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? DocumentFileName { get; set; }

    [MaxLength(150)]
    public string? DocumentMimeType { get; set; }

    public long? DocumentFileSizeBytes { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Something the team has undertaken to achieve, with a measure and a date.
/// </summary>
/// <remarks>
/// <para><b>⚠ <see cref="ProgressPercent"/> is derived when <see cref="ProgressMode"/> is
/// <c>FromTasks</c>.</b> The service recomputes it on every task change under the objective and a
/// caller cannot set it. A field that accepts input and is then overwritten is the defect lane D1
/// spent its budget removing from the probation form.</para>
///
/// <para><b>Completing an objective needs either 100 % or an <see cref="OutcomeSummary"/></b> —
/// the feedback asked for "whether they have done it, and how they did it", and a team that closes
/// an objective at 60 % owes the second answer.</para>
/// </remarks>
public class TeamObjective : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    /// <summary>Short reference, unique within the team — "OBJ-01". Optional.</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>How anyone will know it was achieved — the indicator, in words.</summary>
    [MaxLength(1000)]
    public string? Measure { get; set; }

    /// <summary>The number to reach, where the measure has one.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TargetValue { get; set; }

    /// <summary>What the target is counted in — "incidents", "%", "days".</summary>
    [MaxLength(50)]
    public string? Unit { get; set; }

    /// <summary>
    /// How much of the team's effort this objective represents.
    /// </summary>
    /// <remarks>
    /// ⚠ ADVISORY. The service warns when a team's active weights do not sum to 100 but does not
    /// refuse — a team mid-planning has every right to a half-built set, and a rule that blocks the
    /// second objective until the tenth is written is a rule nobody can work with.
    /// </remarks>
    [Range(0, 100)]
    public int? Weight { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>The member accountable for it. A team member, not any employee.</summary>
    public Guid? OwnerMemberId { get; set; }

    [ForeignKey(nameof(OwnerMemberId))]
    public virtual TeamMember? OwnerMember { get; set; }

    public TeamObjectiveStatus Status { get; set; } = TeamObjectiveStatus.Draft;

    [Range(0, 100)]
    public int ProgressPercent { get; set; }

    public TeamObjectiveProgressMode ProgressMode { get; set; } = TeamObjectiveProgressMode.FromTasks;

    /// <summary>How the team actually achieved it — required to complete below 100 %.</summary>
    [MaxLength(4000)]
    public string? OutcomeSummary { get; set; }

    public DateOnly? CompletedOn { get; set; }

    [MaxLength(1000)]
    public string? CancelledReason { get; set; }

    public virtual ICollection<TeamTask> Tasks { get; set; } = new List<TeamTask>();
}

/// <summary>
/// A piece of work on a team, optionally under an objective.
/// </summary>
/// <remarks>
/// ⚠ <see cref="ObjectiveId"/> is nullable on purpose. Plenty of real committee work — "circulate
/// the minutes", "chase the vendor" — belongs to no objective, and forcing every task under one
/// would either invent objectives nobody wanted or push the work out of the system entirely.
/// </remarks>
public class TeamTask : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    /// <summary>The objective this serves, where it serves one.</summary>
    public Guid? ObjectiveId { get; set; }

    [ForeignKey(nameof(ObjectiveId))]
    public virtual TeamObjective? Objective { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>Who is doing it. A team member, so leaving the team takes the assignment with it.</summary>
    public Guid? AssigneeMemberId { get; set; }

    [ForeignKey(nameof(AssigneeMemberId))]
    public virtual TeamMember? AssigneeMember { get; set; }

    public TeamTaskPriority Priority { get; set; } = TeamTaskPriority.Normal;

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public TeamTaskStatus Status { get; set; } = TeamTaskStatus.NotStarted;

    /// <summary>⚠ Required while the status is <c>Blocked</c> — see the enum.</summary>
    [MaxLength(1000)]
    public string? BlockedReason { get; set; }

    public DateOnly? CompletedOn { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    /// <summary>
    /// The meeting decision this task was raised from, where it came out of a meeting.
    /// </summary>
    /// <remarks>
    /// ⚠ A bare nullable id with NO foreign key and NO navigation, because <c>TeamMeetingDecision</c>
    /// arrives in slice F2 and F1 must not depend forward on it. Provenance, not a live reference —
    /// the same shape as <c>EmployeeBenefitEnrollment.SourceBenefitGroupId</c>. F2 adds the
    /// decision's own <c>RaisedTaskId</c> pointing this way, which is the link the screens read.
    /// </remarks>
    public Guid? SourceMeetingDecisionId { get; set; }

    public virtual ICollection<TeamTaskChecklistItem> ChecklistItems { get; set; } = new List<TeamTaskChecklistItem>();

    public virtual ICollection<TeamTaskAttachment> Attachments { get; set; } = new List<TeamTaskAttachment>();
}

/// <summary>
/// A tick on a task's list.
/// </summary>
/// <remarks>
/// ⚠ A FLAT list, and deliberately not a template engine (question Q-7, answered "no"). Four
/// module-bound checklists already exist in this codebase — SHE inspections, staff movements,
/// separation clearance, procurement award verification — and a fifth generic one would be a
/// migration project across three other owners' modules to save a table here.
/// </remarks>
public class TeamTaskChecklistItem : TenantEntity
{
    [Required]
    public Guid TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public virtual TeamTask Task { get; set; } = null!;

    public int DisplayOrder { get; set; }

    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    public Guid? DoneById { get; set; }

    [ForeignKey(nameof(DoneById))]
    public virtual Employee? DoneBy { get; set; }

    public DateTime? DoneAt { get; set; }
}

/// <summary>A file pertaining to a task, through the controlled gate.</summary>
public class TeamTaskAttachment : TenantEntity
{
    [Required]
    public Guid TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public virtual TeamTask Task { get; set; } = null!;

    [MaxLength(300)]
    public string? Title { get; set; }

    // ⚠ Three ids and the metadata, never a path — see the note on TeamTermsOfReference.
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(150)]
    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    public Guid? UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee? UploadedBy { get; set; }
}
