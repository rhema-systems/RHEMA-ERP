using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Medical;

/// <summary>
/// A medical board — a panel convened to rule on one employee's fitness for duty
/// (residue plan G4 / R-15b).
/// </summary>
/// <remarks>
/// <para><b>Why it lives in Medical and not in Leave.</b> The SHE↔Medical ownership boundary settles
/// it: Medical owns clinical records — health profiles, examinations, facilities, physicians — and
/// other modules <b>bridge by reference</b>. A board ruling on fitness is a clinical record, and its
/// findings are medical-grade data. It is gated on <c>HR.Medical.*</c>, not on the HR role.</para>
///
/// <para>⚠ <b>The bridge is ONE-WAY, and keeping it that way is the point.</b> Leave reads a board
/// to satisfy its evidence rule; separation reads one to justify a medical retirement. <b>Neither
/// writes to it.</b> A shared mutable record across three modules is how three modules come to
/// disagree about what a board decided.</para>
///
/// <para><b>It reuses the vocabulary that already existed.</b> <see cref="Outcome"/> is
/// <see cref="MedicalExamResult"/> — <i>Fit / Fit with restrictions / Temporarily unfit / Unfit /
/// Requires further investigation</i> — which is exactly what a board reports, and was already in
/// use on <c>EmployeeMedicalExam</c>. Minting a parallel enum would have let the two drift.</para>
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

    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;

    public MedicalBoardStatus Status { get; set; } = MedicalBoardStatus.Requested;

    /// <summary>
    /// The question the board is asked (round 5, lane K1). Required when a board is requested;
    /// boards recorded before purposes existed read <see cref="MedicalBoardPurpose.Other"/>.
    /// </summary>
    public MedicalBoardPurpose Purpose { get; set; } = MedicalBoardPurpose.Other;

    /// <summary>Why a board was asked for, in the requester's words. Read beside <see cref="Purpose"/>.</summary>
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Who asked for it. ⚠ A bare <c>Guid</c> with no navigation, like <c>ApprovedById</c> and
    /// <c>RecalledById</c> on <c>LeaveRequest</c>: an unpaired navigation to <c>Employee</c> mints a
    /// shadow <c>EmployeeId1</c> column on the other side. Names are resolved by the read.
    /// </summary>
    public Guid? RequestedById { get; set; }

    public DateOnly RequestedOn { get; set; }
    public DateOnly? ConvenedOn { get; set; }

    /// <summary>The clinical record this board is about, when there is one.</summary>
    public Guid? HealthProfileId { get; set; }
    public virtual EmployeeHealthProfile? HealthProfile { get; set; }

    /// <summary>The examination the board considered, when it was based on one.</summary>
    public Guid? BasedOnExamId { get; set; }
    public virtual EmployeeMedicalExam? BasedOnExam { get; set; }

    public Guid? FacilityId { get; set; }
    public virtual HealthcareFacility? Facility { get; set; }

    // ── The recommendation ───────────────────────────────────────────────────────────────────
    //
    // ⚠ Fields on the board rather than a separate entity, deliberately. ONE board produces ONE
    // recommendation: that is what concluding means. A `MedicalBoardRecommendation` table would
    // imply a board can report more than once, and then nothing could answer "what did the board
    // decide?" without choosing between rows. A board that needs to revisit its own finding is a
    // NEW board, which is also how it works on paper.

    /// <summary>The finding. Null until the board concludes.</summary>
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

    /// <summary>
    /// The board recommends retirement on medical grounds.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>A recommendation, not an act.</b> Nothing here retires anybody: separation owns that,
    /// and <c>SeparationReason.MedicalRetirement</c> already existed for it. This flag is what a
    /// separation can point at to say why. Kept as its own field rather than inferred from
    /// <c>Outcome == Unfit</c>, because a board can find somebody unfit for their current post and
    /// fit for redeployment — which is a different recommendation entirely.
    /// </remarks>
    public bool RecommendsMedicalRetirement { get; set; }

    public DateOnly? ConcludedOn { get; set; }
    public Guid? ConcludedById { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// When it was stopped (round 5, lane K5). Whether that was a cancelled request or a dissolved
    /// board is read from <see cref="ConvenedOn"/>: stopped before convening, it was never a panel.
    /// </summary>
    public DateOnly? CancelledOn { get; set; }

    /// <summary>Who stopped it. A bare <c>Guid</c>, like <see cref="RequestedById"/> and for the same reason.</summary>
    public Guid? CancelledById { get; set; }

    public virtual ICollection<MedicalBoardMember> Members { get; set; } = new List<MedicalBoardMember>();
    public virtual ICollection<MedicalBoardSitting> Sittings { get; set; } = new List<MedicalBoardSitting>();
    public virtual ICollection<MedicalBoardDocument> Documents { get; set; } = new List<MedicalBoardDocument>();

    /// <summary>
    /// Whether a board asked this question rules on an ABSENCE — the only kind a leave type's board
    /// threshold can rest on (round 5, lane K6).
    /// </summary>
    /// <remarks>
    /// ⚠ The one place this list lives: the leave evidence gate reads it, and the DTO carries its
    /// answer so no screen keeps a copy. A board asked whether somebody is fit for their post, or
    /// should retire, has not ruled on an absence, however recent it is.
    /// </remarks>
    public static bool CoversAbsence(MedicalBoardPurpose purpose) =>
        purpose is MedicalBoardPurpose.ExtendedSickLeave
                or MedicalBoardPurpose.InjuryOnDuty
                or MedicalBoardPurpose.Other;
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
/// <para>All three are recorded rather than flattened to a string, because a model that only took
/// names would throw away the link for the people who ARE on file — and "who sat on the board" is
/// most of what its authority rests on.</para>
///
/// <para>⚠ <b>The <see cref="Employee"/> navigation is safe here, unlike the board's own actor
/// columns.</b> <c>EmployeeId</c> and <c>Employee</c> pair by EF convention, so no shadow FK is
/// minted. <c>MedicalBoard.RequestedById</c> and <c>ConcludedById</c> are deliberately bare Guids
/// for the opposite reason: a second, differently-named navigation to <c>Employee</c> on the same
/// entity cannot be paired and mints an <c>EmployeeId1</c> column on the other side.</para>
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
    /// ⚠ The service refuses to seat the <b>subject of the board</b>. Nobody sits in judgement on
    /// their own fitness, and a record showing they did would discredit the finding.
    /// </remarks>
    public Guid? EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }

    /// <summary>Used when the member is neither on the physician register nor an employee.</summary>
    [MaxLength(200)]
    public string? MemberName { get; set; }

    /// <summary>Where the member is from — a hospital, a union, a department.</summary>
    [MaxLength(200)]
    public string? Institution { get; set; }

    public MedicalBoardMemberRole Role { get; set; } = MedicalBoardMemberRole.Member;
}

/// <summary>One meeting of a medical board.</summary>
/// <remarks>
/// A board may sit more than once before it reports — hence its own rows rather than a single date
/// on the board. The <b>recommendation</b> still belongs to the board, not to a sitting: the last
/// sitting is where it was agreed, but it is the board that gives it.
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
