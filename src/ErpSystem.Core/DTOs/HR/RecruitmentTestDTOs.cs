using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Round 4, lane E — recruitment tests.
//
//  ⚠ TWO PROJECTIONS OF A QUESTION, and the split is the most important thing in this file:
//
//    RecruitmentTestQuestionDto           — the AUTHORING view. Carries IsCorrect, ExpectedAnswer
//                                           and Explanation. HR only.
//    CandidateTestQuestionDto             — the SITTING view. Carries none of them.
//
//  A single DTO with "don't populate those fields for candidates" is the guard that gets forgotten
//  the third time somebody adds a read. Orientation's portal documents the same rule; here it is a
//  type, so forgetting is a compile error rather than a leak.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

#region Authoring

public class RecruitmentTestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public JobApplicantTestType TestType { get; set; }
    public string TestTypeName => TestType.ToString();
    public int? DurationMinutes { get; set; }
    public decimal? PassMarkPercent { get; set; }
    public int MaxAttempts { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public bool IsActive { get; set; }

    public int QuestionCount { get; set; }
    public decimal TotalPoints { get; set; }

    /// <summary>
    /// True once anybody has sat it, which freezes the paper.
    /// </summary>
    /// <remarks>
    /// ⚠ The screen must disable editing on this, not merely hope. Changing a question after it has
    /// been answered rewrites what somebody was marked on.
    /// </remarks>
    public bool HasSittings { get; set; }

    public List<RecruitmentTestSectionDto> Sections { get; set; } = new();
    public List<RecruitmentTestQuestionDto> Questions { get; set; } = new();
}

public class RecruitmentTestSectionDto : BaseDto
{
    public Guid RecruitmentTestId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>⚠ THE AUTHORING VIEW. Never served to a candidate — see the file header.</summary>
public class RecruitmentTestQuestionDto : BaseDto
{
    public Guid RecruitmentTestId { get; set; }
    public Guid? RecruitmentTestSectionId { get; set; }
    public string? SectionName { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public RecruitmentQuestionType QuestionType { get; set; }
    public string QuestionTypeName => QuestionType.ToString();
    public decimal Points { get; set; }
    public string? ExpectedAnswer { get; set; }
    public string? Explanation { get; set; }
    public int DisplayOrder { get; set; }
    public List<RecruitmentTestQuestionOptionDto> Options { get; set; } = new();
}

/// <summary>⚠ Carries <see cref="IsCorrect"/>. Authoring and the marking key only.</summary>
public class RecruitmentTestQuestionOptionDto : BaseDto
{
    public Guid RecruitmentTestQuestionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateRecruitmentTestDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(4000)] public string? Instructions { get; set; }

    public JobApplicantTestType TestType { get; set; } = JobApplicantTestType.Written;

    [Range(1, 600)] public int? DurationMinutes { get; set; }
    [Range(0, 100)] public decimal? PassMarkPercent { get; set; }
    [Range(1, 10)] public int MaxAttempts { get; set; } = 1;

    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
}

public class UpdateRecruitmentTestDto : CreateRecruitmentTestDto
{
    [Required] public Guid Id { get; set; }
}

public class UpdateRecruitmentTestSectionDto : CreateRecruitmentTestSectionDto
{
    [Required] public Guid Id { get; set; }
}

public class CreateRecruitmentTestSectionDto
{
    [Required] public Guid RecruitmentTestId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateRecruitmentTestQuestionDto
{
    [Required] public Guid RecruitmentTestId { get; set; }
    public Guid? RecruitmentTestSectionId { get; set; }

    [Required, MaxLength(2000)] public string QuestionText { get; set; } = string.Empty;

    public RecruitmentQuestionType QuestionType { get; set; } = RecruitmentQuestionType.SingleChoice;

    [Range(0, 100)] public decimal Points { get; set; } = 1;

    [MaxLength(500)] public string? ExpectedAnswer { get; set; }
    [MaxLength(2000)] public string? Explanation { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// The choices, as the whole set.
    /// </summary>
    /// <remarks>
    /// ⚠ A replace-set on update, like the shortlisting criterion values: every save carries the
    /// whole list and a row left out is removed. Omitting the list on a closed question is refused
    /// rather than read as "no options" — see the service.
    /// </remarks>
    public List<CreateRecruitmentTestQuestionOptionDto>? Options { get; set; }
}

public class UpdateRecruitmentTestQuestionDto : CreateRecruitmentTestQuestionDto
{
    [Required] public Guid Id { get; set; }
}

public class CreateRecruitmentTestQuestionOptionDto
{
    [Required, MaxLength(1000)] public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}

#endregion

#region Assignment

public class RecruitmentTestAssignmentDto : BaseDto
{
    public Guid RecruitmentTestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string TestCode { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }

    public Guid? JobVacancyId { get; set; }
    public string? VacancyNumber { get; set; }
    public string? JobTitle { get; set; }

    public Guid? JobApplicationId { get; set; }
    public string? ApplicationNumber { get; set; }
    public string? CandidateName { get; set; }

    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public bool IsRequired { get; set; }
    public int ExtraAttemptsGranted { get; set; }
    public string? ExtraAttemptReason { get; set; }
    public DateTime? InvitedAt { get; set; }

    public int SittingCount { get; set; }
}

public class CreateRecruitmentTestAssignmentDto
{
    [Required] public Guid RecruitmentTestId { get; set; }

    /// <summary>
    /// ⚠ Exactly ONE of these. Both, or neither, is refused: a vacancy-wide assignment and a
    /// per-application one answer different questions, and a row that is both is neither.
    /// </summary>
    public Guid? JobVacancyId { get; set; }
    public Guid? JobApplicationId { get; set; }

    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public bool IsRequired { get; set; } = true;
}

public class GrantExtraAttemptDto
{
    [Required] public Guid AssignmentId { get; set; }

    [Range(1, 5)] public int ExtraAttempts { get; set; } = 1;

    /// <summary>⚠ Required. Decision Q4: a re-sit is granted with a reason on record, or not at all.</summary>
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Sitting — the candidate's side

/// <summary>
/// ⚠ THE SITTING VIEW of a question. No <c>IsCorrect</c>, no <c>ExpectedAnswer</c>, no
/// <c>Explanation</c> — see the file header for why this is a separate type.
/// </summary>
public class CandidateTestQuestionDto
{
    public Guid Id { get; set; }
    public Guid? SectionId { get; set; }
    public string? SectionName { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public RecruitmentQuestionType QuestionType { get; set; }
    public string QuestionTypeName => QuestionType.ToString();
    public decimal Points { get; set; }
    public int DisplayOrder { get; set; }
    public List<CandidateTestOptionDto> Options { get; set; } = new();
}

/// <summary>⚠ The option text and nothing else. No <c>IsCorrect</c>.</summary>
public class CandidateTestOptionDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>The paper as a candidate sees it, plus where their attempt has got to.</summary>
public class CandidateSittingDto
{
    public Guid SittingId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int? DurationMinutes { get; set; }
    public int AttemptNumber { get; set; }
    public RecruitmentSittingStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When this attempt must be in by — fixed when the candidate STARTED.
    /// </summary>
    /// <remarks>
    /// ⚠ The client counts down to this, and the SERVER enforces it at submit. A timer that exists
    /// only in the browser is a suggestion. Fixed at start rather than recomputed, so closing the
    /// tab and coming back tomorrow does not hand out a fresh clock.
    /// </remarks>
    public DateTime? MustSubmitBy { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// The attempt's session token, returned ONCE per open and required to write to it.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Only the hash is stored (SHA-256, with the last four characters kept in clear so a
    /// support call can identify a session) — procurement's onboarding-token design, which § 9.4
    /// recommends HR borrow. A raw token in a table is a bearer credential anybody with database
    /// access can use.</para>
    ///
    /// <para>⚠ It binds ONE live session to the attempt, and opening the attempt again re-issues it.
    /// Two tabs on the same test is otherwise a real way to lose work: the stale tab's autosave
    /// carries the answers as they were ten minutes ago and overwrites the fresh ones. The stale tab
    /// is refused instead, and told why.</para>
    /// </remarks>
    public string? AccessToken { get; set; }

    /// <summary>What the candidate has already answered, so a refresh mid-test resumes.</summary>
    public List<CandidateSavedAnswerDto> SavedAnswers { get; set; } = new();

    public List<CandidateTestQuestionDto> Questions { get; set; } = new();
}

public class CandidateSavedAnswerDto
{
    public Guid QuestionId { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = new();
    public string? FreeTextAnswer { get; set; }
    public string? NumericAnswer { get; set; }
}

/// <summary>
/// What the candidate has, for the assessments tab of the careers portal.
/// </summary>
public class CandidateAssessmentSummaryDto
{
    public Guid AssignmentId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;

    public string TestName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public int QuestionCount { get; set; }
    public bool IsRequired { get; set; }

    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }

    /// <summary>True when the window is open right now AND an attempt remains.</summary>
    public bool CanStart { get; set; }

    /// <summary>
    /// Why not, when <see cref="CanStart"/> is false.
    /// </summary>
    /// <remarks>
    /// ⚠ Carried rather than left to the client to work out. A disabled button with no reason is
    /// the commonest complaint in every screen walk this programme has run: the candidate cannot
    /// tell "not open yet" from "you have used your attempts" from "we are still marking it".
    /// </remarks>
    public string? BlockedReason { get; set; }

    public int AttemptsUsed { get; set; }
    public int AttemptsAllowed { get; set; }

    /// <summary>The attempt in progress, if there is one — the resume link.</summary>
    public Guid? InProgressSittingId { get; set; }

    public RecruitmentSittingStatus? LastStatus { get; set; }
    public string? LastStatusName => LastStatus?.ToString();
    public DateTime? LastSubmittedAt { get; set; }

    /// <summary>
    /// ⚠ Only ever set once the sitting is finalised, and never for one still being marked. A
    /// provisional mark shown to a candidate is a number they will hold the organisation to.
    /// </summary>
    public decimal? ReleasedScorePercent { get; set; }
    public bool? Passed { get; set; }
}

/// <summary>
/// The shape used BOTH to save progress mid-test and to submit.
/// </summary>
/// <remarks>
/// One type on purpose: a save and a submit carry exactly the same thing, and two types that must
/// stay identical is one type that will not.
/// </remarks>
public class SubmitSittingDto
{
    [Required] public Guid SittingId { get; set; }

    /// <summary>
    /// The token the open handed back. ⚠ Required — see <see cref="CandidateSittingDto.AccessToken"/>.
    /// </summary>
    [Required, MaxLength(200)]
    public string AccessToken { get; set; } = string.Empty;

    public List<SubmitAnswerDto> Answers { get; set; } = new();
}

/// <summary>What the candidate is told the moment they submit.</summary>
/// <remarks>
/// ⚠ <see cref="ScorePercent"/> is NULL while anything is awaiting a human, and the screen says so
/// rather than showing a mark that is missing the essay.
/// </remarks>
public class CandidateSittingResultDto
{
    public Guid SittingId { get; set; }
    public RecruitmentSittingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? SubmittedAt { get; set; }
    public bool AwaitingMarking { get; set; }

    /// <summary>Set only when the paper was settled outright — no free text, nothing to wait for.</summary>
    public decimal? ScorePercent { get; set; }
    public bool? Passed { get; set; }

    /// <summary>True when the clock ran out and the sitting was marked on what had been saved.</summary>
    public bool TimedOut { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class SubmitAnswerDto
{
    [Required] public Guid QuestionId { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = new();
    [MaxLength(4000)] public string? FreeTextAnswer { get; set; }
    [MaxLength(100)] public string? NumericAnswer { get; set; }
}

#endregion

#region Sitting — HR's side

public class RecruitmentTestSittingDto : BaseDto
{
    public Guid RecruitmentTestAssignmentId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }
    public RecruitmentSittingStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? MustSubmitBy { get; set; }

    public decimal? AutoScore { get; set; }
    public decimal? ManualScore { get; set; }
    public decimal? FinalScore { get; set; }
    public decimal? TotalPoints { get; set; }
    public decimal? ScorePercent { get; set; }
    public bool? Passed { get; set; }

    public DateTime? MarkedAt { get; set; }
    public string? MarkerNotes { get; set; }

    /// <summary>Set once the sitting has produced a row in the result ledger.</summary>
    public Guid? JobApplicantTestResultId { get; set; }

    /// <summary>True while free-text answers are waiting for a human.</summary>
    public bool AwaitingManualMarking { get; set; }

    public List<SittingAnswerDto> Answers { get; set; } = new();
}

/// <summary>One answer, as the marker sees it — with the verdict.</summary>
public class SittingAnswerDto
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public RecruitmentQuestionType QuestionType { get; set; }
    public string QuestionTypeName => QuestionType.ToString();
    public decimal QuestionPoints { get; set; }

    public Guid? SelectedOptionId { get; set; }
    public string? SelectedOptionText { get; set; }
    public string? FreeTextAnswer { get; set; }
    public string? NumericAnswer { get; set; }

    public bool IsCorrect { get; set; }
    public decimal PointsAwarded { get; set; }
    public bool IsManuallyMarked { get; set; }
    public string? MarkerComment { get; set; }
}

public class MarkFreeTextAnswerDto
{
    [Required] public Guid AnswerId { get; set; }

    [Range(0, 100)] public decimal PointsAwarded { get; set; }

    [MaxLength(1000)] public string? MarkerComment { get; set; }
}

public class FinaliseSittingDto
{
    [Required] public Guid SittingId { get; set; }

    [MaxLength(2000)] public string? MarkerNotes { get; set; }
}

#endregion
