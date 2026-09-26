using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// A medical board — the panel — with its cases, members, sittings and papers (residue plan G4;
/// round 5, lanes K and K-II-a).
/// </summary>
/// <remarks>
/// ⚠ <b>Medical-grade content.</b> Findings, recommendations and sitting notes describe somebody's
/// health. Every endpoint returning this is gated on <c>HR.Medical.*</c>, not on the HR role — the same
/// treatment the SHE↔Medical boundary gave occupational-health surveillance and return-to-work plans.
/// </remarks>
public class MedicalBoardDto
{
    public Guid Id { get; set; }
    public string BoardNumber { get; set; } = string.Empty;

    /// <summary>The employer's own board, or one of the Workmen's Compensation Act's statutory boards.</summary>
    public MedicalBoardKind Kind { get; set; }

    public MedicalBoardStatus Status { get; set; }

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateOnly RequestedOn { get; set; }
    public DateOnly? ConvenedOn { get; set; }

    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }

    /// <summary>When the board reported — its last open case closed with at least one decided.</summary>
    public DateOnly? ConcludedOn { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>When it was stopped, and by whom (round 5, lane K5). Null on boards stopped before K5.</summary>
    public DateOnly? CancelledOn { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }

    /// <summary>
    /// ⚠ Stopped after it was convened: the board was <b>dissolved</b>. Stopped before, it was a
    /// <b>cancelled request</b> — one status, two words, told apart by whether it was ever a panel.
    /// </summary>
    public bool WasDissolved { get; set; }

    /// <summary>The employees before the board, one case each (round 5, lane K-II-a).</summary>
    public List<MedicalBoardCaseDto> Cases { get; set; } = new();
    public List<MedicalBoardMemberDto> Members { get; set; } = new();
    public List<MedicalBoardSittingDto> Sittings { get; set; } = new();
}

/// <summary>One employee's case before a board, and its finding (round 5, lane K-II-a).</summary>
public class MedicalBoardCaseDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string BoardNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>The question the board is asked about this employee (round 5, lane K1).</summary>
    public MedicalBoardPurpose Purpose { get; set; }

    /// <summary>
    /// ⚠ Whether this purpose can satisfy a leave type's board rule (lane K6) —
    /// <c>MedicalBoardCase.CoversAbsence</c>'s answer, carried so no screen keeps its own copy of the list.
    /// </summary>
    public bool CoversAbsence { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateOnly RequestedOn { get; set; }

    public Guid? HealthProfileId { get; set; }
    public Guid? BasedOnExamId { get; set; }

    /// <summary>The examination the case was based on, read back so the page can name it (lane K3).</summary>
    public DateOnly? BasedOnExamDate { get; set; }
    public MedicalExamResult? BasedOnExamResult { get; set; }

    public MedicalBoardCaseStatus Status { get; set; }

    /// <summary>The sitting the case was decided at, and its date.</summary>
    public Guid? DecidedAtSittingId { get; set; }
    public DateOnly? DecidedAtSittingDate { get; set; }

    /// <summary>
    /// ⚠ Who decided: that sitting's attendance, members removed since included. Empty for a case
    /// decided before attendance was recorded (lane K-II-a) — the page says so.
    /// </summary>
    public List<string> DecidedBy { get; set; } = new();

    public MedicalExamResult? Outcome { get; set; }
    public string? Findings { get; set; }
    public string? Recommendation { get; set; }
    public string? Restrictions { get; set; }
    public DateOnly? ReviewDueDate { get; set; }

    /// <summary>⚠ A recommendation, not an act — separation decides, this only says what was advised.</summary>
    public bool RecommendsMedicalRetirement { get; set; }

    public DateOnly? ConcludedOn { get; set; }
    public Guid? ConcludedById { get; set; }
    public string? ConcludedByName { get; set; }

    public DateOnly? WithdrawnOn { get; set; }
    public Guid? WithdrawnById { get; set; }
    public string? WithdrawnByName { get; set; }
    public string? WithdrawalReason { get; set; }

    // ── The injury, its incapacity and compensation (round 5, lane K-II-b) ──────────────────

    /// <summary>The SHE incident, for a case about an injury on duty — read by reference.</summary>
    public Guid? SafetyIncidentId { get; set; }
    public string? SafetyIncidentNumber { get; set; }
    public DateOnly? SafetyIncidentDate { get; set; }

    /// <summary>Notice of the accident and the claim are due within six months of it (s.12). Shown, not enforced.</summary>
    public DateOnly? ClaimNoticeDueBy { get; set; }

    public IncapacityKind? IncapacityKind { get; set; }
    public decimal? IncapacityPercentage { get; set; }
    public DateOnly? IncapacityAssessedOn { get; set; }
    public string? IncapacityAssessedBy { get; set; }
    public string? IncapacityNotes { get; set; }
    public CompensationNotPayableReason? CompensationNotPayableReason { get; set; }
    public List<MedicalBoardCaseInjuryDto> Injuries { get; set; } = new();

    /// <summary>
    /// ⚠ Indicative — the labour officer notifies the amount (s.35), it is paid to the Court (s.11(3)) and
    /// nothing may be set off against it (s.27). Never a settlement line.
    /// </summary>
    public decimal? IndicativeCompensation { get; set; }
    public string? IndicativeCompensationBasis { get; set; }
    public string? CompensationCurrency { get; set; }

    public decimal? NotifiedCompensation { get; set; }
    public DateOnly? CompensationNotifiedOn { get; set; }
    public DateOnly? CompensationDueOn { get; set; }
    public decimal? AgreedCompensation { get; set; }
    public DateOnly? CompensationAgreedOn { get; set; }

    /// <summary>Once the labour officer's amount is recorded the assessment it answered is fixed.</summary>
    public bool AssessmentFixed { get; set; }

    /// <summary>A temporary incapacity is paid for at most this long (s.7(2)(c)) — from the incident.</summary>
    public int? TemporaryIncapacityMaxMonths { get; set; }
    public DateOnly? TemporaryPaymentsEndBy { get; set; }
}

/// <summary>One assessed injury on a case (round 5, lane K-II-b).</summary>
public class MedicalBoardCaseInjuryDto
{
    public Guid Id { get; set; }

    /// <summary>The schedule row it came from; null for the panel's own assessment (s.6(1)(b)).</summary>
    public Guid? ScheduleItemId { get; set; }
    public IncapacityScheduleKind? ScheduleKind { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>The row's percentage when assessed — kept, so editing the schedule later moves nothing.</summary>
    public decimal BasePercentage { get; set; }
    public LossOfUse LossOfUse { get; set; }
    public bool NonDominantSide { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// The incapacity assessment of a case — the whole assessment, replacing the last (round 5, lane K-II-b).
/// </summary>
/// <remarks>
/// ⚠ <b>The injuries are the whole set</b>: leaving one out removes it (the replace-set convention).
/// Allowed on a listed or decided case until the labour officer's amount is recorded.
/// </remarks>
public class AssessIncapacityDto
{
    /// <summary>Required. Send either permanent value — which one follows from the percentage (s.38).</summary>
    public IncapacityKind? Kind { get; set; }

    public List<AssessedInjuryDto>? Injuries { get; set; }

    /// <summary>Today when omitted; never in the future.</summary>
    public DateOnly? AssessedOn { get; set; }

    /// <summary>Required: the attending medical officer (s.2(3)).</summary>
    [MaxLength(200)]
    public string? AssessedBy { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public CompensationNotPayableReason? NotPayableReason { get; set; }
}

/// <summary>One injury in an assessment: a schedule row, or the panel's own figure.</summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>A Third Schedule row</b>: the row's percentage, with <see cref="LossOfUse"/> (partial = 50 %)
///   and, on an arm or hand, <see cref="NonDominantSide"/> (90 %). <see cref="Percentage"/> is not sent.</item>
///   <item><b>A First Schedule (disfigurement) row</b>: <see cref="Percentage"/> up to the row's — the
///   row's own when omitted (s.8).</item>
///   <item><b>No row</b>: the panel's own assessment of lost earning capacity (s.6(1)(b)) —
///   <see cref="Description"/> and <see cref="Percentage"/> required.</item>
/// </list>
/// </remarks>
public class AssessedInjuryDto
{
    public Guid? ScheduleItemId { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public decimal? Percentage { get; set; }
    public LossOfUse? LossOfUse { get; set; }
    public bool NonDominantSide { get; set; }
}

/// <summary>What the labour officer notified, and any agreement (round 5, lane K-II-b) — the whole record.</summary>
public class RecordCompensationDto
{
    public decimal? NotifiedCompensation { get; set; }
    public DateOnly? NotifiedOn { get; set; }

    /// <summary>Three months after the notification when omitted (s.35).</summary>
    public DateOnly? DueOn { get; set; }

    /// <summary>Never below the Act's amount (s.15): the notified amount, or else the indicative one.</summary>
    public decimal? AgreedCompensation { get; set; }
    public DateOnly? AgreedOn { get; set; }
}

/// <summary>A row of a tenant's compensation schedule (round 5, lane K-II-b).</summary>
public class IncapacityScheduleItemDto
{
    public Guid Id { get; set; }
    public IncapacityScheduleKind Kind { get; set; }
    public string Injury { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool AppliesToArmOrHand { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Adding or changing a schedule row. ⚠ Assessments already made keep the percentage they used.</summary>
public class SaveIncapacityScheduleItemDto
{
    public IncapacityScheduleKind? Kind { get; set; }

    [MaxLength(300)]
    public string Injury { get; set; } = string.Empty;

    public decimal? Percentage { get; set; }

    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;

    public bool AppliesToArmOrHand { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class MedicalBoardMemberDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid? PhysicianId { get; set; }

    /// <summary>Set when the member works here — HR, a staff or union representative, a nurse.</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>The registered name when there is one, otherwise the name typed in.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Which of the three a member is, so a screen can show it without guessing.</summary>
    public string MemberKind { get; set; } = string.Empty;

    public string? Institution { get; set; }
    public MedicalBoardMemberRole Role { get; set; }

    /// <summary>Chair or member: counted towards the quorum at a deciding sitting (lane K-II-a).</summary>
    public bool Decides { get; set; }
}

public class MedicalBoardSittingDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public DateOnly SittingDate { get; set; }
    public string? Venue { get; set; }
    public string? Notes { get; set; }

    /// <summary>The members present (round 5, lane K-II-a). Removed members keep their attendance.</summary>
    public List<MedicalBoardAttendeeDto> Attendees { get; set; } = new();

    /// <summary>Cases decided at this sitting — its attendance is then fixed.</summary>
    public int CasesDecided { get; set; }
}

public class MedicalBoardAttendeeDto
{
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public MedicalBoardMemberRole Role { get; set; }
    public bool Decides { get; set; }
}

/// <summary>Asking for a board, with its first case.</summary>
/// <remarks>
/// <para>⚠ <see cref="Purpose"/> is nullable so that leaving it out is a refusal the service can
/// word, not a silent 0 — <c>[Required]</c> on a non-nullable enum checks nothing.</para>
///
/// <para>⚠ <b>The references are checked, not stored blind</b> (round 5, lane K3): the facility must
/// be this tenant's; the health profile and the examination must be the subject's own. Name an
/// examination and the profile follows from it; name neither and the subject's own profile is used.</para>
///
/// <para>More employees are added with <c>POST {id}/cases</c> (lane K-II-a).</para>
/// </remarks>
public class RequestMedicalBoardDto
{
    /// <summary>The employer's own board unless said otherwise.</summary>
    public MedicalBoardKind? Kind { get; set; }

    public Guid? FacilityId { get; set; }

    /// <summary>The first case: the employee.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Required. The question the board is asked about this employee.</summary>
    public MedicalBoardPurpose? Purpose { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? HealthProfileId { get; set; }
    public Guid? BasedOnExamId { get; set; }
}

/// <summary>Another employee before the same board (round 5, lane K-II-a).</summary>
public class AddMedicalBoardCaseDto
{
    public Guid EmployeeId { get; set; }

    /// <summary>Required, as on the first case.</summary>
    public MedicalBoardPurpose? Purpose { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? HealthProfileId { get; set; }
    public Guid? BasedOnExamId { get; set; }
}

/// <summary>
/// Appointing somebody to a board.
/// </summary>
/// <remarks>
/// <para>⚠ Supply <b>exactly one</b> of <see cref="PhysicianId"/>, <see cref="EmployeeId"/> or
/// <see cref="MemberName"/> — two at once is refused (round 5, lane K7): a row naming a physician
/// AND typing somebody else's name would say two people sat in one seat. A board is not only
/// doctors — it carries HR as secretary, a union or staff representative, and often a clinician from
/// outside who is in nobody's register.</para>
///
/// <para>⚠ Nobody who is a case before the board can be seated on it.</para>
/// </remarks>
public class AddMedicalBoardMemberDto
{
    public Guid? PhysicianId { get; set; }

    /// <summary>A member who works here.</summary>
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? MemberName { get; set; }

    [MaxLength(200)]
    public string? Institution { get; set; }

    public MedicalBoardMemberRole Role { get; set; } = MedicalBoardMemberRole.Member;
}

public class RecordMedicalBoardSittingDto
{
    public DateOnly SittingDate { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    /// <summary>The members present — each must be on the board (round 5, lane K-II-a).</summary>
    public List<Guid>? AttendeeMemberIds { get; set; }
}

/// <summary>Who was present at a sitting, replacing what was recorded (round 5, lane K-II-a).</summary>
/// <remarks>⚠ Refused once a case has been decided at the sitting: its attendance is then the panel that decided.</remarks>
public class SetMedicalBoardSittingAttendanceDto
{
    public List<Guid> MemberIds { get; set; } = new();
}

/// <summary>
/// A case decided. This is the write that makes it count for anything (round 5, lane K-II-a).
/// </summary>
public class ConcludeMedicalBoardCaseDto
{
    /// <summary>
    /// Required: the sitting it was decided at. Its attendance must include the quorum of deciding
    /// members — who decided is who was there.
    /// </summary>
    public Guid? SittingId { get; set; }

    /// <summary>
    /// Required — a case that concludes without a finding has not concluded.
    /// </summary>
    /// <remarks>
    /// ⚠ Nullable so that leaving it out is refused (round 5, lane K7). As a plain enum an omitted
    /// outcome bound to 0 — no finding at all.
    /// </remarks>
    public MedicalExamResult? Outcome { get; set; }

    [MaxLength(4000)]
    public string? Findings { get; set; }

    [Required, MaxLength(4000)]
    public string Recommendation { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Restrictions { get; set; }

    public DateOnly? ReviewDueDate { get; set; }
    public bool RecommendsMedicalRetirement { get; set; }
}

public class MedicalBoardFilterDto
{
    /// <summary>Boards with a case about this employee.</summary>
    public Guid? EmployeeId { get; set; }
    public MedicalBoardStatus? Status { get; set; }

    /// <summary>Boards with a case asked this question.</summary>
    public MedicalBoardPurpose? Purpose { get; set; }

    public MedicalBoardKind? Kind { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Search { get; set; }
}

/// <summary>A paper on a board (round 5, lane K4). Served only by the board's own download.</summary>
public class MedicalBoardDocumentDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }

    /// <summary>The case the paper is about, when it is about one employee (lane K-II-a).</summary>
    public Guid? CaseId { get; set; }
    public string? CaseEmployeeName { get; set; }

    public string FileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }
}
