using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffGrievance;

/// <summary>
/// An employee grievance, and its progress up FR-HR-181's escalation ladder.
/// </summary>
/// <remarks>
/// <para><b>What this is, and what it is not.</b> FR-HR-181 requires that a grievance escalate
/// through Employee → Supervisor → HOD → HR → GM Finance &amp; Administration → Managing Director →
/// Board, retaining the statement and each level's response. That is what this models: the record and
/// the ladder. It is a FIRST CUT — case conferencing, union representation, anonymous intake and
/// grievance analytics belong to an employee-relations module that does not exist yet, and this store
/// becomes its data when it is built. See [[hr-deferred-modules]] #2.</para>
///
/// <para><b>Why it is not on the workflow engine</b>, unlike the disciplinary decision. The engine
/// models approval — a thing is proposed and someone with authority confirms or refuses it. A
/// grievance is not approved; it is ANSWERED, and the person answering may resolve it, or may fail to
/// satisfy the employee, who then escalates. The decision to move up the ladder belongs to the
/// GRIEVER, not to an approver, which is the opposite of the engine's shape. Same class of call as
/// [[goal-approval-stays-bespoke]].</para>
///
/// <para><b>Why the rungs are not resolved to people.</b> TDC's org data cannot support it — see the
/// note on <see cref="GrievanceEscalationLevel"/>. A grievance sits at a rung; whoever answers is
/// recorded from their own token, and HR may name a responder explicitly on a step so that person can
/// see and answer it. Routing can be added later without changing any of this.</para>
/// </remarks>
public class StaffGrievance : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GrievanceNumber { get; set; } = string.Empty;

    /// <summary>
    /// What kind of employee-relations case this is — area 9c slice 1, decision D-4.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="EmployeeRelationsCaseType.Grievance"/>, which is also enum member 1,
    /// so every row written before this column existed is correct with no back-fill. ⚠ The type is
    /// descriptive, not a permission boundary: read access is still decided entirely by who is on
    /// the case.
    /// </remarks>
    public EmployeeRelationsCaseType CaseType { get; set; } = EmployeeRelationsCaseType.Grievance;

    /// <summary>
    /// The employee raising it — and, since area 9c, the case's PRIMARY PARTY.
    /// </summary>
    /// <remarks>
    /// Always the token's employee on the grievance path; never supplied in a payload, which is why
    /// HR cannot raise a grievance on somebody's behalf. It stays <b>required</b> under decision
    /// D-7: every employee-relations case at TDC concerns at least one identifiable employee, and a
    /// case about a class of staff names the affected employee or the union representative as
    /// primary. Everybody else on the case is a <see cref="StaffGrievanceParty"/>.
    /// </remarks>
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The grievance statement in the employee's own words. FR-HR-181 requires it be retained, so
    /// nothing overwrites it after filing — later positions are recorded as step responses.
    /// </summary>
    [Required]
    [MaxLength(6000)]
    public string Statement { get; set; } = string.Empty;

    public DateTime FiledDate { get; set; }

    public GrievanceStatus Status { get; set; } = GrievanceStatus.Filed;

    /// <summary>The rung the grievance is currently sitting at, awaiting an answer.</summary>
    public GrievanceEscalationLevel CurrentLevel { get; set; } = GrievanceEscalationLevel.Supervisor;

    public DateTime? ResolvedDate { get; set; }

    [MaxLength(4000)]
    public string? ResolutionSummary { get; set; }

    public DateTime? WithdrawnDate { get; set; }

    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }

    public virtual ICollection<StaffGrievanceStep> Steps { get; set; } = new List<StaffGrievanceStep>();

    /// <summary>Everybody on the case other than the primary party — area 9c slice 1.</summary>
    public virtual ICollection<StaffGrievanceParty> Parties { get; set; } = new List<StaffGrievanceParty>();
}

/// <summary>
/// Somebody involved in an employee-relations case besides the primary party — area 9c slice 1,
/// decision D-7.
/// </summary>
/// <remarks>
/// <para><b>Why the party may be external.</b> Representation at a grievance is very often by a
/// union official or a lawyer who is not on the payroll, and FR-HR-181 requires union consultation
/// be retained. Modelling every party as an <c>Employee</c> FK would have made the commonest real
/// representative unrecordable, so exactly one of <see cref="EmployeeId"/> and
/// <see cref="ExternalName"/> is supplied and the service refuses both or neither.</para>
///
/// <para><b>⚠ Being a party is not by itself a right to read the case.</b> The read rule set in
/// area 9 slice 7 — the primary party, HR, or somebody named on a step — is deliberately narrower
/// than "anyone involved", because a case is usually ABOUT somebody and the respondent must not be
/// handed the complainant's statement by being added to it. Slice 1 does not widen it. What a
/// respondent is owed is a matter of natural justice handled by the disclosure the process makes,
/// not by a row in this table.</para>
/// </remarks>
public class StaffGrievanceParty : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievancePartyRole Role { get; set; }

    /// <summary>Set when the party is a member of staff. Mutually exclusive with <see cref="ExternalName"/>.</summary>
    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    /// <summary>Set when the party is not a member of staff — a union official, a lawyer.</summary>
    [MaxLength(200)]
    public string? ExternalName { get; set; }

    /// <summary>The body an external party comes from, when it is not the union named below.</summary>
    [MaxLength(200)]
    public string? ExternalOrganisation { get; set; }

    /// <summary>
    /// Whom this party acts for. Set for a representative or union representative; null everywhere
    /// else. Points at an employee rather than at another party row, because the person most often
    /// represented is the primary party, who has no party row of their own.
    /// </summary>
    public Guid? RepresentsEmployeeId { get; set; }

    [ForeignKey(nameof(RepresentsEmployeeId))]
    public virtual Employee? RepresentsEmployee { get; set; }

    /// <summary>The recognised union a union representative acts for. FR-HR-181's union thread.</summary>
    public Guid? UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union? Union { get; set; }

    public DateTime AddedDate { get; set; }

    /// <summary>Who added them, from their own token — HR, in every path that exists today.</summary>
    public Guid? AddedById { get; set; }

    [ForeignKey(nameof(AddedById))]
    public virtual Employee? AddedBy { get; set; }

    /// <summary>Why they are on the case. Not the substance of the case — that lives on the steps.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Cleared when a party stands down — a representative replaced, a witness withdrawn. Kept as a
    /// date rather than a delete so the case file still reads correctly for the period they acted.
    /// </summary>
    public DateTime? RemovedDate { get; set; }

    [MaxLength(500)]
    public string? RemovalReason { get; set; }
}

/// <summary>
/// One rung of the ladder: who was asked, what they said, and what happened next.
/// </summary>
/// <remarks>
/// A step is created when the grievance ARRIVES at a rung, not when it is answered — so an unanswered
/// step is the record of who currently owes a response, and the ladder reads as a history even while
/// it is still running. Steps are append-only: escalating does not amend the level below, because
/// FR-HR-181 requires each level's response be retained.
/// </remarks>
public class StaffGrievanceStep : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievanceEscalationLevel Level { get; set; }

    /// <summary>Order within the grievance, so the trail reads correctly even if two share a level.</summary>
    public int Sequence { get; set; }

    public DateTime ReachedDate { get; set; }

    /// <summary>
    /// Optional. HR may name who should answer at this rung — which is how a supervisor or head of
    /// department participates without the system having to derive who they are. Naming someone also
    /// lets them see and answer this grievance; nobody else outside HR can.
    /// </summary>
    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }

    [MaxLength(4000)]
    public string? Response { get; set; }

    public DateTime? RespondedDate { get; set; }

    /// <summary>Whoever actually answered, from their own token.</summary>
    public Guid? RespondedById { get; set; }

    [ForeignKey(nameof(RespondedById))]
    public virtual Employee? RespondedBy { get; set; }

    public GrievanceStepOutcome Outcome { get; set; } = GrievanceStepOutcome.AwaitingResponse;
}

// =============================================================================
// REMINDER ENGINE (area 9 slice 8)
//
// The area computes a great many queues and tells nobody about any of them: the
// 48-hour written query, the four-week investigation, hearings coming up, the
// five-working-day appeal window, appeals past their ten working days, overdue
// corrective actions, expiring warnings, unpaid fines, and grievances sitting at a
// rung nobody has answered. Every one of those is a deadline visible only to
// someone who happens to open the right screen on the right day — and in this area
// a missed deadline is not an inconvenience, it is the thing that makes a sanction
// or a dismissal indefensible.
//
// Structure mirrors the staff-movement engine (area 8 slice 5), which mirrors SHE's
// (area 10 slice 13): all logic in the service so the daily host and the HR-gated
// run-now endpoint share exactly one code path, and a dispatch log whose unique
// (TenantId, DedupeKey) index is the send-once guarantee.
//
// They live in this file rather than the discipline entities file because the sweep
// covers BOTH halves of the area — disciplinary cases and grievances — and putting
// it with the narrower of the two would misdescribe it.
// =============================================================================

/// <summary>One execution of the discipline reminder sweep, scheduled or run by hand.</summary>
public class DisciplineReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<DisciplineReminderDispatchLog> DispatchLogs { get; set; }
        = new List<DisciplineReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
/// </summary>
/// <remarks>
/// The unique (TenantId, DedupeKey) index is the send-once guarantee: a key encodes the item, the
/// reminder kind, the due date and the ladder rung or escalation tier reached, so each rung fires
/// exactly once — and moving a due date re-arms the ladder, because it produces fresh keys.
///
/// ⚠ Nothing here carries the allegation, the grievance statement, or the employee's name. A
/// reminder travels further than the record it is about — into notification lists and, one day,
/// email — so it says a case number and a deadline and makes the reader open the record to learn
/// anything else. The same reasoning governs the workflow display resolver for this area.
/// </remarks>
public class DisciplineReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual DisciplineReminderRun Run { get; set; } = null!;

    /// <summary>Machine kind, e.g. "WrittenQueryDue", "AppealDecisionOverdue", "GrievanceUnanswered".</summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Disciplinary case", "Grievance".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What the notification shows: the case or grievance number, and nothing more.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}
