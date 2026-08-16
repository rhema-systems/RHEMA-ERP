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

    /// <summary>The employee raising it. Always the token's employee — never supplied in a payload.</summary>
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
