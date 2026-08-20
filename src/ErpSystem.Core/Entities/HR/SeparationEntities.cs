using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// AREA 9b — SEPARATION, CLEARANCE & EXIT
//
// FRD §A1.10 (FR-HR-090/091/092/093) and §3.A.2 "Separation & Final Settlement"
// (FR-HR-182/183/184/185).
//
// This is the ONE exit record, for every route out of the organisation. Before it, the only
// way an employee could leave was through a disciplinary case: StaffDisciplineTermination and
// StaffDisciplineSeparation both hang off a DisciplinaryActionId, so resignation, retirement,
// contract expiry and death had enum members and no route. Measured on the live tenant
// 2026-08-20: 3,693 employees, none ever terminated, and 29 disciplinary terminations whose
// employees were all still StaffStatus = Active because nothing propagated the outcome.
//
// The disciplinary route does not get its own parallel store — it creates one of these too, via
// DisciplinaryActionId. Two exit stores that can disagree is exactly how the 29 orphans happened.
// =============================================================================

/// <summary>
/// One employee leaving the organisation, by any route: the register FR-HR-090 asks for and the
/// spine the clearance run (FR-HR-183), approval (FR-HR-092) and final settlement (FR-HR-184)
/// hang off.
/// </summary>
/// <remarks>
/// <para><b>Separation type versus reason.</b> <see cref="SeparationType"/> is the route out and
/// drives behaviour — what notice applies, who signs, whether a settlement is due.
/// <see cref="ReasonCategory"/> and <see cref="ReasonNotes"/> record why, and are reporting, not
/// logic. Keeping them apart is what lets "resignation" and "resignation to avoid dismissal" be
/// the same process with different analytics.</para>
///
/// <para><b>The record is opened before it is decided.</b> A separation exists from the moment it
/// is raised — a resignation letter received, a retirement date reached, a disciplinary outcome
/// recorded — and moves through clearance, approval and settlement. That is why
/// <see cref="EffectiveDate"/> is nullable at creation: the date someone actually stops being an
/// employee is an outcome of the process, not an input to it.</para>
/// </remarks>
public class EmployeeSeparation : TenantEntity
{
    /// <summary>
    /// Human-readable reference, e.g. <c>SEP-2026-00001</c>. Assigned on create.
    /// </summary>
    /// <remarks>
    /// ⚠ Sequential and therefore enumerable — a by-number lookup walks the range and harvests who
    /// is leaving, which is precisely the information an exit register must not leak. There is
    /// deliberately <b>no</b> by-number endpoint; lookups are by id, and the number is a display
    /// and search field only.
    /// </remarks>
    [Required]
    [MaxLength(30)]
    public string SeparationNumber { get; set; } = string.Empty;

    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>The route out — see FR-HR-182. Drives notice, approval and settlement.</summary>
    [Required]
    public EmployeeTerminationType SeparationType { get; set; }

    [Required]
    public SeparationStatus Status { get; set; } = SeparationStatus.Draft;

    /// <summary>Why the employee is leaving. Reporting only; <see cref="SeparationType"/> drives behaviour.</summary>
    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    // ── Dates ────────────────────────────────────────────────────────────────

    /// <summary>When the separation was raised in the system.</summary>
    [Required]
    public DateOnly InitiatedOn { get; set; }

    /// <summary>
    /// When notice was given — the employee's resignation letter, or the organisation's notice of
    /// termination. Null for routes that carry no notice: death, summary dismissal, contract expiry.
    /// </summary>
    public DateOnly? NoticeGivenOn { get; set; }

    /// <summary>
    /// Notice days that apply, defaulted from <c>CompanyHrPolicySettings</c>
    /// (<c>DefaultResignationNoticeDays</c> / <c>DefaultTerminationNoticeDays</c>, both 30) and
    /// overridable per separation. Feeds notice pay in the FR-HR-184 settlement when notice is
    /// not served.
    /// </summary>
    public int? NoticeDays { get; set; }

    /// <summary>Last day the employee actually attends work. May precede <see cref="EffectiveDate"/> where leave is taken in lieu.</summary>
    public DateOnly? LastWorkingDay { get; set; }

    /// <summary>
    /// The day employment ends. **Null until the separation is approved** — it is an outcome of the
    /// process, not an input. For compulsory retirement this is the birthday itself (FR-HR-093).
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    // ── Who raised it ────────────────────────────────────────────────────────

    /// <summary>
    /// The employee who raised the separation. Null when the system raised it — a retirement date
    /// reached or a contract expiring is nobody's act, and recording a person there would be a lie
    /// the audit trail cannot distinguish from a real one.
    /// </summary>
    public Guid? InitiatedById { get; set; }

    [ForeignKey(nameof(InitiatedById))]
    public virtual Employee? InitiatedBy { get; set; }

    /// <summary>True when the system raised this — the retirement or contract-expiry sweep.</summary>
    public bool IsSystemInitiated { get; set; }

    // ── FR-HR-092: who signs ─────────────────────────────────────────────────

    /// <summary>
    /// True when this separation is <i>procedural</i> and HR may approve it without the MD's
    /// signature (FR-HR-092). Settled with the user 2026-08-20: the FRD's example — absence beyond
    /// ten days — is taken as the whole list, so everything else, resignation and retirement
    /// included, goes to the MD. Set by the service from policy, never by the client, so widening
    /// the list later is a settings change rather than a new trust boundary.
    /// </summary>
    public bool IsProcedural { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [MaxLength(2000)]
    public string? ApprovalNotes { get; set; }

    /// <summary>Set when the approval runs through the workflow engine (slice 4).</summary>
    public Guid? WorkflowInstanceId { get; set; }

    // ── Rehire ───────────────────────────────────────────────────────────────

    public bool IsEligibleForRehire { get; set; } = true;

    public DateOnly? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    // ── The disciplinary route (D1: one pipeline, not two) ───────────────────

    /// <summary>
    /// Set when this separation came out of a disciplinary case, linking to the
    /// <c>StaffDisciplinaryAction</c> that decided it. Area 9 keeps owning the decision and the
    /// hearing record; the exit itself lives here, so the register, the clearance run and the
    /// settlement are the same for a dismissal as for a resignation.
    /// </summary>
    public Guid? DisciplinaryActionId { get; set; }

    // ── The employee master record ───────────────────────────────────────────

    /// <summary>
    /// When this separation was actually applied to the employee's master record — status,
    /// termination date, contracts and position history.
    /// </summary>
    /// <remarks>
    /// ⚠ This column exists because of a measured defect, not for tidiness. Before area 9b, 29
    /// disciplinary terminations had been recorded while all 29 employees remained
    /// <c>StaffStatus = Active</c>: the outcome was written somewhere nobody read. A null here on a
    /// completed separation is that bug, visible in one query instead of a join across three
    /// tables.
    /// </remarks>
    public DateTime? EmployeeRecordUpdatedOn { get; set; }

    // ── Cancellation ─────────────────────────────────────────────────────────

    public DateTime? CancelledOn { get; set; }

    public Guid? CancelledById { get; set; }

    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
}
