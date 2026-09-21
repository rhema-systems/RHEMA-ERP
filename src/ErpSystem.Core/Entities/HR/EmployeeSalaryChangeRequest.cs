using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A request to change what an employee is paid, approved on the workflow engine and APPLIED by
/// the service when approved (round 3, lane S; decision D-1).
///
/// <para>Before this, the Salary tab had three direct writes — the pay basis, the grade placement
/// and payroll's monthly basic — and nothing between an officer and a raise. Now, when the policy
/// setting <c>SalaryChangeRequiresApproval</c> is on, those doors refuse and point here; an approved
/// request writes the same things through the same doors with an <see cref="SalaryChangeAuthority.Approved"/>
/// authority, so the rule has one implementation and two callers. Movements and hire-from-offer keep
/// writing directly: they are approved records already.</para>
///
/// <para>Three kinds, one record. <b>Placement</b> moves somebody on the scale to a grade / level /
/// notch. <b>NegotiatedAmount</b> sets the agreed figure for somebody paid off the scale.
/// <b>PayBasisSwitch</b> moves between the two and carries whichever figure the new basis needs.
/// The current terms are snapshotted at raise so the approver reads what was true when asked, and
/// so the request still reads sensibly after it is applied.</para>
///
/// <para>Applying is two halves. HR's half (basis, placement, the record figure) is written first
/// and stamped in <see cref="HrAppliedOn"/>; payroll's half — the monthly basic, through payroll's
/// own upsert — second. A payroll failure lands on <see cref="SalaryChangeRequestStatus.AwaitingPayrollEntry"/>
/// with the reason in <see cref="ApplyFailure"/>, and a retry re-runs only what is still owed.</para>
/// </summary>
public class EmployeeSalaryChangeRequest : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    public SalaryChangeKind Kind { get; set; }

    public SalaryChangeRequestStatus Status { get; set; } = SalaryChangeRequestStatus.Draft;

    // ── The terms when the request was raised ─────────────────────────────

    public PayBasis CurrentPayBasis { get; set; }
    public Guid? CurrentGradeId { get; set; }
    public Guid? CurrentLevelId { get; set; }
    public Guid? CurrentNotchId { get; set; }

    /// <summary>HR's monthly basic pay as resolved when the request was raised (notch, or the negotiated figure).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CurrentAmount { get; set; }

    // ── What is proposed ──────────────────────────────────────────────────

    /// <summary>Set for <see cref="SalaryChangeKind.PayBasisSwitch"/> only; the basis after the change.</summary>
    public PayBasis? ProposedPayBasis { get; set; }

    public Guid? ProposedGradeId { get; set; }
    public Guid? ProposedLevelId { get; set; }
    public Guid? ProposedNotchId { get; set; }

    /// <summary>The negotiated monthly amount, for the kinds that carry one.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ProposedAmount { get; set; }

    [MaxLength(10)]
    public string? ProposedCurrencyCode { get; set; }

    public DateTime EffectiveDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Who raised it — an Employee, because the engine's initiator is a user and the record's actor is a person.</summary>
    public Guid RequestedById { get; set; }

    /// <summary>The post-appraisal <c>SalaryReviewProposal</c> this was raised from, when it was.</summary>
    public Guid? SourceProposalId { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    /// <summary>HR's half written: basis, placement, the record figure.</summary>
    public DateTime? HrAppliedOn { get; set; }

    /// <summary>Everything written, payroll included.</summary>
    public DateTime? AppliedOn { get; set; }

    public Guid? AppliedByUserId { get; set; }

    /// <summary>The placement the request wrote, when it wrote one.</summary>
    public Guid? AppliedPlacementId { get; set; }

    /// <summary>The last reason applying stopped short — HR's half or payroll's. Cleared when it goes through.</summary>
    [MaxLength(1000)]
    public string? ApplyFailure { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee? RequestedBy { get; set; }

    [ForeignKey(nameof(ProposedGradeId))]
    public virtual SalaryGrade? ProposedGrade { get; set; }

    [ForeignKey(nameof(ProposedLevelId))]
    public virtual SalaryLevel? ProposedLevel { get; set; }

    [ForeignKey(nameof(ProposedNotchId))]
    public virtual SalaryNotch? ProposedNotch { get; set; }
}
