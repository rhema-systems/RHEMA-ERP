using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Medical;

/// <summary>
/// A medical board — a PANEL convened to rule on employees' health (residue plan G4 / R-15b; since
/// round 5 lane K-II-a, a panel that hears <see cref="MedicalBoardCase"/>s).
/// </summary>
/// <remarks>
/// <para><b>Why it lives in Medical and not in Leave.</b> The SHE↔Medical ownership boundary settles
/// it: Medical owns clinical records — health profiles, examinations, facilities, physicians — and
/// other modules <b>bridge by reference</b>. A board ruling on fitness is a clinical record, and its
/// findings are medical-grade data. It is gated on <c>HR.Medical.*</c>, not on the HR role.</para>
///
/// <para>⚠ <b>The bridge is ONE-WAY, and keeping it that way is the point.</b> Leave reads a case
/// to satisfy its evidence rule; separation reads one to justify a medical retirement. <b>Neither
/// writes to it.</b> A shared mutable record across three modules is how three modules come to
/// disagree about what a board decided.</para>
///
/// <para><b>A panel that hears cases (lane K-II-a) — the standard shape.</b> Tribunals, disciplinary
/// panels, credentialing committees and occupational-health boards all separate the panel (who sits,
/// when it met, its papers) from the cases before it (one per person, each with its own finding). One
/// sitting can decide several people; each finding records the sitting it was decided at, and that
/// sitting's attendance is who decided. Until K-II-a the board WAS its one employee's case, and the
/// finding columns lived here; they moved to <see cref="MedicalBoardCase"/>.</para>
/// </remarks>
public class MedicalBoard : TenantEntity
{
    /// <summary>Human-readable reference, e.g. <c>MB-20260917213455123</c>.</summary>
    /// <remarks>
    /// ⚠ Timestamped rather than sequential, matching <c>MedicalReferral.ReferralNumber</c> in the
    /// same module. Two reasons beyond consistency: a counted sequence repeats after a soft delete
    /// unless it is hardened (the trap company schedule C-6 records for events and bookings), and a
    /// sequential board number is enumerable — walking the range would harvest who is being assessed
    /// for fitness, which is the one thing this register must not leak. Same reasoning
    /// <c>SeparationNumber</c> carries about its own by-number lookup.
    /// </remarks>
    [MaxLength(40)]
    public string BoardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Who convened it: the employer, or one of the two statutory boards of the Workmen's
    /// Compensation Act (lane K-II-a).
    /// </summary>
    public MedicalBoardKind Kind { get; set; } = MedicalBoardKind.Employer;

    public MedicalBoardStatus Status { get; set; } = MedicalBoardStatus.Requested;

    /// <summary>
    /// Who asked for the board. ⚠ A bare <c>Guid</c> with no navigation: an unpaired navigation to
    /// <c>Employee</c> mints a shadow <c>EmployeeId1</c> column on the other side. Names are resolved by the read.
    /// </summary>
    public Guid? RequestedById { get; set; }

    public DateOnly RequestedOn { get; set; }
    public DateOnly? ConvenedOn { get; set; }

    /// <summary>Where the board sits.</summary>
    public Guid? FacilityId { get; set; }
    public virtual HealthcareFacility? Facility { get; set; }

    /// <summary>
    /// When the board reported — the day its last open case closed with at least one decided.
    /// Each case carries its own decision date.
    /// </summary>
    public DateOnly? ConcludedOn { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// When it was stopped (round 5, lane K5). Whether that was a cancelled request or a dissolved
    /// board is read from <see cref="ConvenedOn"/>: stopped before convening, it was never a panel.
    /// </summary>
    public DateOnly? CancelledOn { get; set; }

    /// <summary>Who stopped it. A bare <c>Guid</c>, like <see cref="RequestedById"/> and for the same reason.</summary>
    public Guid? CancelledById { get; set; }

    public virtual ICollection<MedicalBoardCase> Cases { get; set; } = new List<MedicalBoardCase>();
    public virtual ICollection<MedicalBoardMember> Members { get; set; } = new List<MedicalBoardMember>();
    public virtual ICollection<MedicalBoardSitting> Sittings { get; set; } = new List<MedicalBoardSitting>();
    public virtual ICollection<MedicalBoardDocument> Documents { get; set; } = new List<MedicalBoardDocument>();
}

/// <summary>
/// One employee's case before a medical board, and its finding (round 5, lane K-II-a).
/// </summary>
/// <remarks>
/// <para><b>One employee, one case, per board</b> (a unique index). A board hearing several people
/// has several cases; each is decided on its own, at a recorded sitting.</para>
///
/// <para><b>It reuses the vocabulary that already existed.</b> <see cref="Outcome"/> is
/// <see cref="MedicalExamResult"/> — <i>Fit / Fit with restrictions / Temporarily unfit / Unfit /
/// Requires further investigation</i> — which is exactly what a board reports, and was already in
/// use on <c>EmployeeMedicalExam</c>. Minting a parallel enum would have let the two drift.</para>
///
/// <para>⚠ <b>The finding is fields on the case, not a table.</b> One case, one finding: that is what
/// concluding means. A case that needs revisiting is a NEW case, which is also how it works on
/// paper — and it is what stops leave having been approved on a finding since edited away.</para>
/// </remarks>
public class MedicalBoardCase : TenantEntity
{
    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual MedicalBoard Board { get; set; } = null!;

    /// <summary>The employee the case is about. Unique per board.</summary>
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>The question the board is asked about this employee (round 5, lane K1).</summary>
    public MedicalBoardPurpose Purpose { get; set; } = MedicalBoardPurpose.Other;

    /// <summary>Why, in the requester's words.</summary>
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Who listed the case. A bare <c>Guid</c>; names are resolved by the read.</summary>
    public Guid? RequestedById { get; set; }
    public DateOnly RequestedOn { get; set; }

    /// <summary>The clinical record the case is about, when there is one (lane K3).</summary>
    public Guid? HealthProfileId { get; set; }
    public virtual EmployeeHealthProfile? HealthProfile { get; set; }

    /// <summary>The examination the case is based on — the subject's own (lane K3).</summary>
    public Guid? BasedOnExamId { get; set; }
    public virtual EmployeeMedicalExam? BasedOnExam { get; set; }

    public MedicalBoardCaseStatus Status { get; set; } = MedicalBoardCaseStatus.Listed;

    // ── The finding ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The sitting the case was decided at — its attendance is the panel that decided.
    /// </summary>
    /// <remarks>
    /// ⚠ A bare <c>Guid</c>, checked by the service to be one of this board's sittings. A foreign key
    /// would be a second path from the board into this table (the board cascades to its sittings and
    /// to its cases), which SQL Server refuses.
    /// </remarks>
    public Guid? DecidedAtSittingId { get; set; }

    /// <summary>The finding. Null until the case is decided.</summary>
    public MedicalExamResult? Outcome { get; set; }

    [MaxLength(4000)]
    public string? Findings { get; set; }

    [MaxLength(4000)]
    public string? Recommendation { get; set; }

    /// <summary>Restrictions on returning to work, when the outcome is fit-with-restrictions.</summary>
    [MaxLength(2000)]
    public string? Restrictions { get; set; }

    /// <summary>When the board says the employee should be looked at again.</summary>
    public DateOnly? ReviewDueDate { get; set; }

    /// <summary>The board recommends retirement on medical grounds.</summary>
    /// <remarks>
    /// ⚠ <b>A recommendation, not an act.</b> Nothing here retires anybody: separation owns that, and
    /// <c>SeparationReason.MedicalRetirement</c> already existed for it. Kept as its own field rather
    /// than inferred from <c>Outcome == Unfit</c>, because a board can find somebody unfit for their
    /// current post and fit for redeployment — a different recommendation entirely.
    /// </remarks>
    public bool RecommendsMedicalRetirement { get; set; }

    public DateOnly? ConcludedOn { get; set; }
    public Guid? ConcludedById { get; set; }

    // ── Withdrawn without a finding ──────────────────────────────────────────────────────────

    public DateOnly? WithdrawnOn { get; set; }
    public Guid? WithdrawnById { get; set; }

    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }

    // ── The injury, its incapacity and compensation (round 5, lane K-II-b; PNDCL 187) ────────

    /// <summary>
    /// The SHE incident where the injury was reported, for a case about an injury on duty. A bare
    /// <c>Guid</c> across the SHE↔Medical boundary — referenced, never navigated or written.
    /// </summary>
    public Guid? SafetyIncidentId { get; set; }

    /// <summary>Null until assessed. ⚠ Permanent partial/total is derived from the percentage (s.38).</summary>
    public IncapacityKind? IncapacityKind { get; set; }

    /// <summary>The injuries' percentages summed, capped at 100 (s.6(2)). Null unless permanent.</summary>
    /// <remarks>
    /// ⚠ No column type is declared on these decimals: <c>ApplicationDbContext.ConfigureDecimalPrecision</c>
    /// sets every decimal to <c>decimal(18,4)</c> (18,2 when the name holds Cost, Price, Amount, Total or
    /// Salary) and overrides any attribute or <c>HasPrecision</c>. The service rounds money to 2 places.
    /// </remarks>
    public decimal? IncapacityPercentage { get; set; }

    public DateOnly? IncapacityAssessedOn { get; set; }

    /// <summary>The attending medical officer whose assessment the figure rests on (s.2(3)).</summary>
    [MaxLength(200)]
    public string? IncapacityAssessedBy { get; set; }

    [MaxLength(2000)]
    public string? IncapacityNotes { get; set; }

    /// <summary>Set when the Act excludes compensation (s.2(5), (7), (8)).</summary>
    public CompensationNotPayableReason? CompensationNotPayableReason { get; set; }

    /// <summary>
    /// The indicative figure, worked out when the assessment is recorded and kept as worked out — later
    /// pay or setting changes do not move it. ⚠ Indicative: the labour officer notifies the amount due
    /// (s.35), it is paid to the Court (s.11(3)), and nothing may be set off against it (s.27).
    /// </summary>
    public decimal? IndicativeCompensation { get; set; }

    [MaxLength(1000)]
    public string? IndicativeCompensationBasis { get; set; }

    [MaxLength(3)]
    public string? CompensationCurrency { get; set; }

    /// <summary>What the chief labour officer notified (s.35). Once recorded, the assessment is fixed.</summary>
    public decimal? NotifiedCompensation { get; set; }

    public DateOnly? CompensationNotifiedOn { get; set; }

    /// <summary>Payable within three months of the notification (s.35).</summary>
    public DateOnly? CompensationDueOn { get; set; }

    /// <summary>An agreement in writing (s.15) — never below the Act's amount.</summary>
    public decimal? AgreedCompensation { get; set; }

    public DateOnly? CompensationAgreedOn { get; set; }

    public virtual ICollection<MedicalBoardCaseInjury> Injuries { get; set; } = new List<MedicalBoardCaseInjury>();

    /// <summary>
    /// Whether a case asked this question rules on an ABSENCE — the only kind a leave type's board
    /// threshold can rest on (round 5, lane K6).
    /// </summary>
    /// <remarks>
    /// ⚠ The one place this list lives: the leave evidence gate reads it, and the DTO carries its
    /// answer so no screen keeps a copy. A case asking whether somebody is fit for their post, or
    /// should retire, has not ruled on an absence, however recent it is.
    /// </remarks>
    public static bool CoversAbsence(MedicalBoardPurpose purpose) =>
        purpose is MedicalBoardPurpose.ExtendedSickLeave
                or MedicalBoardPurpose.InjuryOnDuty
                or MedicalBoardPurpose.Other;
}

/// <summary>
/// One assessed injury on a case (round 5, lane K-II-b): a schedule row, or the panel's own
/// assessment of lost earning capacity for an injury the Schedule does not name (s.6(1)(b)).
/// </summary>
/// <remarks>
/// ⚠ <b>The row's percentage is copied, not referenced.</b> An administrator may later edit the schedule;
/// a finding already made must not move with it. <see cref="ScheduleItemId"/> says which row it came from.
/// </remarks>
public class MedicalBoardCaseInjury : TenantEntity
{
    public Guid CaseId { get; set; }

    [ForeignKey(nameof(CaseId))]
    public virtual MedicalBoardCase Case { get; set; } = null!;

    /// <summary>The schedule row, or null for the panel's own assessment.</summary>
    public Guid? ScheduleItemId { get; set; }
    public virtual IncapacityScheduleItem? ScheduleItem { get; set; }

    /// <summary>The injury as named — the row's words, or the panel's.</summary>
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>The row's percentage when assessed, or the panel's assessed percentage.</summary>
    public decimal BasePercentage { get; set; }

    public LossOfUse LossOfUse { get; set; } = LossOfUse.Total;

    /// <summary>An arm or hand on the side the employee does not favour: ninety percent (Third Schedule note).</summary>
    public bool NonDominantSide { get; set; }

    /// <summary>After loss of use and dominance: what this injury adds to the case.</summary>
    public decimal Percentage { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// A row of a compensation schedule, per tenant (round 5, lane K-II-b) — loaded from PNDCL 187's First
/// and Third Schedules (<c>docs/HR/catalogues/HR-WORKMENS-COMPENSATION-SCHEDULES.md</c>) and editable,
/// so a client under another schedule names its own rows and their source.
/// </summary>
public class IncapacityScheduleItem : TenantEntity
{
    public IncapacityScheduleKind Kind { get; set; } = IncapacityScheduleKind.Incapacity;

    [MaxLength(300)]
    public string Injury { get; set; } = string.Empty;

    /// <summary>
    /// The percentage of permanent total incapacity. ⚠ For a disfigurement row it is the MOST that may be
    /// assessed (s.8: a practitioner determines the amount up to it).
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>Where the row comes from, e.g. <c>PNDCL 187, Third Schedule</c>.</summary>
    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;

    /// <summary>An arm or hand: the non-dominant side is rated at ninety percent (Third Schedule note).</summary>
    public bool AppliesToArmOrHand { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

/// <summary>Somebody appointed to a medical board.</summary>
/// <remarks>
/// <para><b>Three ways to be a member, and the service requires exactly one of them to identify
/// you.</b> A board is not only doctors: in practice it carries HR as secretary, a union or staff
/// representative, sometimes the employee's own representative, alongside the clinicians who
/// actually rule on fitness.</para>
///
/// <list type="bullet">
///   <item><see cref="PhysicianId"/> — a clinician on the register.</item>
///   <item><see cref="EmployeeId"/> — somebody who works here. HR, the union representative, an
///   in-house occupational health nurse who is staff rather than a registered physician.</item>
///   <item><see cref="MemberName"/> — anybody else. Boards routinely include a doctor from outside
///   the organisation who is in nobody's register, and insisting otherwise would force somebody to
///   invent a register entry.</item>
/// </list>
///
/// <para>⚠ <b>The <see cref="Employee"/> navigation is safe here, unlike the board's own actor
/// columns.</b> <c>EmployeeId</c> and <c>Employee</c> pair by EF convention, so no shadow FK is
/// minted.</para>
///
/// <para>⚠ <b>Removing a member is a soft delete</b>, and a sitting's attendance keeps naming them: the
/// panel that decided a case is history, not the current membership (lane K-II-a).</para>
/// </remarks>
public class MedicalBoardMember : TenantEntity
{
    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual MedicalBoard Board { get; set; } = null!;

    public Guid? PhysicianId { get; set; }
    public virtual Physician? Physician { get; set; }

    /// <summary>
    /// A member who works here — HR, a staff or union representative, an in-house nurse.
    /// </summary>
    /// <remarks>
    /// ⚠ The service refuses to seat anybody who is <b>a case before the board</b>. Nobody sits in
    /// judgement on their own fitness, and a record showing they did would discredit the finding.
    /// </remarks>
    public Guid? EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }

    /// <summary>Used when the member is neither on the physician register nor an employee.</summary>
    [MaxLength(200)]
    public string? MemberName { get; set; }

    /// <summary>Where the member is from — a hospital, a union, a department.</summary>
    [MaxLength(200)]
    public string? Institution { get; set; }

    /// <summary>
    /// ⚠ Chair and Member DECIDE; Secretary and Observer attend without deciding. The quorum at a
    /// deciding sitting counts the first two (lane K-II-a).
    /// </summary>
    public MedicalBoardMemberRole Role { get; set; } = MedicalBoardMemberRole.Member;
}

/// <summary>One meeting of a medical board.</summary>
/// <remarks>
/// A board may sit more than once, and may decide different cases at different sittings — hence its
/// own rows, and, since lane K-II-a, its own attendance: who was present is who decided whatever was
/// decided there.
/// </remarks>
public class MedicalBoardSitting : TenantEntity
{
    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual MedicalBoard Board { get; set; } = null!;

    public DateOnly SittingDate { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    /// <summary>What was discussed. ⚠ Medical-grade content — this is gated with the rest.</summary>
    [MaxLength(4000)]
    public string? Notes { get; set; }

    public virtual ICollection<MedicalBoardSittingAttendance> Attendance { get; set; } = new List<MedicalBoardSittingAttendance>();
}

/// <summary>
/// A member present at a sitting (round 5, lane K-II-a).
/// </summary>
/// <remarks>
/// ⚠ <see cref="MemberId"/> is a bare <c>Guid</c>, checked by the service to be one of the board's
/// members. A foreign key would give SQL Server two cascade paths from the board into this table
/// (through its sittings and through its members), which it refuses. A member removed later keeps
/// their attendance: it records who sat, not who sits now.
/// </remarks>
public class MedicalBoardSittingAttendance : TenantEntity
{
    public Guid SittingId { get; set; }

    [ForeignKey(nameof(SittingId))]
    public virtual MedicalBoardSitting Sitting { get; set; } = null!;

    public Guid MemberId { get; set; }
}

/// <summary>
/// A paper on a medical board — the referral, a specialist's report the panel read, the signed
/// minutes, the letter standing it down (round 5, lane K4).
/// </summary>
/// <remarks>
/// <para>Uploaded through the controlled-upload gate (scanned, registered in the central DMS as
/// <i>Medical restricted</i>) and served only by the board's own download, after the Medical read
/// check. Never a caller-supplied path — the exam-documents defect this module has already fixed.</para>
///
/// <para>⚠ <b>Added at any status; removed only while the board is open.</b> The signed report
/// usually arrives after the board concludes, so adding must stay possible. Removing one after the
/// board has reported or been stopped would take evidence out from under a finding somebody may
/// already rest on — the same reason membership and sittings freeze.</para>
///
/// <para>The <see cref="UploadedBy"/> navigation pairs with <c>UploadedById</c> by name, so it
/// mints no shadow column.</para>
/// </remarks>
public class MedicalBoardDocument : TenantEntity
{
    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual MedicalBoard Board { get; set; } = null!;

    /// <summary>
    /// The case the paper is about, when it is about one employee (lane K-II-a). A bare <c>Guid</c>,
    /// checked to be this board's, for the same cascade reason as the case's decided-at sitting.
    /// </summary>
    public Guid? CaseId { get; set; }

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }
}
