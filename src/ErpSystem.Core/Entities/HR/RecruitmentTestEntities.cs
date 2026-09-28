using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Recruitment;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Round 4, lane E — recruitment tests and aptitude exams.
//
//  ⚠ Before this, `JobApplicantTestResult` was a RESULT LEDGER and nothing else: a name, a date, a
//  venue, a score somebody typed in, an invigilator. There were no questions, no answers and no
//  delivery — so `JobVacancy.TestScoreWeight`, which blends a test score into the shortlisting
//  score, had nothing to blend and silently did nothing (§ 3 defect 8).
//
//  ⚠ This mirrors ORIENTATION's assessment engine deliberately (decision D-2): that one works, is
//  sat online, is auto-marked, and carries a hard-won grading fix this must not lose — the
//  denominator is every gradable question ON THE PAPER, not just the ones that came back. Answering
//  one of ten and omitting the rest scored 100% there until somebody found it.
//
//  ⚠ The result ledger is KEPT and written to when a sitting is finalised, rather than replaced.
//  Offline tests are real — a practical, a typing test at a desk — and the ledger is where they
//  live. The engine becomes one way of producing a row, not the only way.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A test paper: what it measures, how long it runs, and what counts as a pass.
/// </summary>
/// <remarks>
/// A test is authored once and assigned many times — to a vacancy, or to named applications. The
/// paper and the sitting are separate so that editing next year's paper cannot change what somebody
/// was marked on last year.
/// </remarks>
public class RecruitmentTest : TenantEntity
{
    [MaxLength(50)]
    public string TestCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>What the candidate reads before starting. Rendered above the first question.</summary>
    [MaxLength(4000)]
    public string? Instructions { get; set; }

    public JobApplicantTestType TestType { get; set; } = JobApplicantTestType.Written;

    /// <summary>
    /// How long a sitting may run. Null means untimed.
    /// </summary>
    /// <remarks>
    /// ⚠ Enforced on the SERVER at submit, not only by the client's countdown. A timer that exists
    /// only in the browser is a suggestion.
    /// </remarks>
    public int? DurationMinutes { get; set; }

    /// <summary>The percentage a candidate must reach. Null means the paper does not pass or fail.</summary>
    [Range(0, 100)]
    public decimal? PassMarkPercent { get; set; }

    /// <summary>
    /// How many times one candidate may sit it. Defaults to 1.
    /// </summary>
    /// <remarks>
    /// ⚠ Decision Q4: HR can grant one extra attempt with a reason recorded, which is why the
    /// assignment carries its own allowance rather than this being the last word.
    /// </remarks>
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 1;

    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }

    /// <summary>
    /// ⚠ A test cannot be assigned until it is active, and cannot be edited once a sitting exists
    /// against it — see the service. An inactive test is a draft; a used one is a record.
    /// </summary>
    public bool IsActive { get; set; }

    public virtual ICollection<RecruitmentTestSection> Sections { get; set; } = new List<RecruitmentTestSection>();
    public virtual ICollection<RecruitmentTestQuestion> Questions { get; set; } = new List<RecruitmentTestQuestion>();
}

/// <summary>An optional grouping of questions — "Numerical reasoning", "Situational judgement".</summary>
/// <remarks>
/// Optional on purpose: a twenty-question aptitude test needs no sections, and forcing one would
/// mean every paper carrying a section called "Questions".
/// </remarks>
public class RecruitmentTestSection : TenantEntity
{
    public Guid RecruitmentTestId { get; set; }

    [ForeignKey(nameof(RecruitmentTestId))]
    public virtual RecruitmentTest Test { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public virtual ICollection<RecruitmentTestQuestion> Questions { get; set; } = new List<RecruitmentTestQuestion>();
}

/// <summary>One question on a paper.</summary>
public class RecruitmentTestQuestion : TenantEntity
{
    public Guid RecruitmentTestId { get; set; }

    [ForeignKey(nameof(RecruitmentTestId))]
    public virtual RecruitmentTest Test { get; set; } = null!;

    /// <summary>The section it sits in, where the paper has sections.</summary>
    public Guid? RecruitmentTestSectionId { get; set; }

    [ForeignKey(nameof(RecruitmentTestSectionId))]
    public virtual RecruitmentTestSection? Section { get; set; }

    [Required]
    [MaxLength(2000)]
    public string QuestionText { get; set; } = string.Empty;

    public RecruitmentQuestionType QuestionType { get; set; } = RecruitmentQuestionType.SingleChoice;

    /// <summary>What this question is worth. The paper's total is the sum over gradable questions.</summary>
    [Range(0, 100)]
    public decimal Points { get; set; } = 1;

    /// <summary>
    /// The expected answer for a Numeric question, and the marking note for a FreeText one.
    /// </summary>
    /// <remarks>
    /// ⚠ NEVER served to a candidate. The participant read must not expose this any more than it
    /// exposes <c>IsCorrect</c> — the guard the orientation portal already documents.
    /// </remarks>
    [MaxLength(500)]
    public string? ExpectedAnswer { get; set; }

    /// <summary>Shown after marking, so a candidate learns something. Not served during the sitting.</summary>
    [MaxLength(2000)]
    public string? Explanation { get; set; }

    public int DisplayOrder { get; set; }

    public virtual ICollection<RecruitmentTestQuestionOption> Options { get; set; }
        = new List<RecruitmentTestQuestionOption>();
}

/// <summary>One choice on a closed question.</summary>
public class RecruitmentTestQuestionOption : TenantEntity
{
    public Guid RecruitmentTestQuestionId { get; set; }

    [ForeignKey(nameof(RecruitmentTestQuestionId))]
    public virtual RecruitmentTestQuestion Question { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string OptionText { get; set; } = string.Empty;

    /// <summary>
    /// ⚠ The single most sensitive column in this file. It must never reach a candidate: the
    /// participant projection strips it, and the marking key that shows it is HR-gated.
    /// </summary>
    public bool IsCorrect { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// A test put in front of somebody: either everybody applying for a vacancy, or named applications.
/// </summary>
/// <remarks>
/// <para>⚠ Exactly one of <see cref="JobVacancyId"/> and <see cref="JobApplicationId"/> is set. A
/// vacancy-wide assignment reaches every live application; a per-application one reaches one
/// candidate, which is how a re-sit or a late applicant is handled.</para>
///
/// <para>The window is the assignment's, not the test's: the same paper can open for one vacancy in
/// March and another in September.</para>
/// </remarks>
public class RecruitmentTestAssignment : TenantEntity
{
    public Guid RecruitmentTestId { get; set; }

    [ForeignKey(nameof(RecruitmentTestId))]
    public virtual RecruitmentTest Test { get; set; } = null!;

    public Guid? JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy? JobVacancy { get; set; }

    public Guid? JobApplicationId { get; set; }

    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication? JobApplication { get; set; }

    /// <summary>When the test may be sat. Null bounds mean "open now" and "no closing date".</summary>
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }

    /// <summary>
    /// Whether the candidate must sit it. An optional test still blends into the score if taken.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Extra attempts granted beyond the test's <see cref="RecruitmentTest.MaxAttempts"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ Decision Q4 — HR may grant a re-sit, and the reason is recorded. Kept on the ASSIGNMENT
    /// rather than the test so that granting one candidate another go does not silently give
    /// everybody one.
    /// </remarks>
    [Range(0, 10)]
    public int ExtraAttemptsGranted { get; set; }

    [MaxLength(1000)]
    public string? ExtraAttemptReason { get; set; }

    public DateTime? InvitedAt { get; set; }

    public virtual ICollection<RecruitmentTestSitting> Sittings { get; set; } = new List<RecruitmentTestSitting>();
}

/// <summary>One candidate's attempt at a paper.</summary>
public class RecruitmentTestSitting : TenantEntity
{
    public Guid RecruitmentTestAssignmentId { get; set; }

    [ForeignKey(nameof(RecruitmentTestAssignmentId))]
    public virtual RecruitmentTestAssignment Assignment { get; set; } = null!;

    /// <summary>
    /// The application this sitting belongs to.
    /// </summary>
    /// <remarks>
    /// ⚠ Carried on the sitting even when the assignment is vacancy-wide: a vacancy assignment
    /// produces one sitting per candidate, and without this the sitting could not say whose it is.
    /// </remarks>
    public Guid JobApplicationId { get; set; }

    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;

    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// The single-use token the candidate's link carries.
    /// </summary>
    /// <remarks>
    /// ⚠ Stored as a SHA-256 hash with only the last four characters kept in clear, following the
    /// design § 9.4 recommends HR borrow from procurement's onboarding token. A raw token in a
    /// table is a bearer credential anybody with database access can use.
    /// </remarks>
    [MaxLength(200)]
    public string? AccessTokenHash { get; set; }

    [MaxLength(8)]
    public string? AccessTokenLast4 { get; set; }

    public DateTime? AccessTokenExpiresAt { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// When the sitting must be in by, fixed at START from the test's duration.
    /// </summary>
    /// <remarks>
    /// ⚠ Computed once, when the candidate starts, and stored. Recomputing it at submit from
    /// "now minus duration" would let somebody close the tab and come back tomorrow with a full
    /// clock — which is what "resume on refresh" would otherwise mean.
    /// </remarks>
    public DateTime? MustSubmitBy { get; set; }

    public RecruitmentSittingStatus Status { get; set; } = RecruitmentSittingStatus.NotStarted;

    /// <summary>
    /// Online, or sat on paper and entered by HR (lane E6).
    /// </summary>
    /// <remarks>
    /// ⚠ Defaults to <see cref="RecruitmentSittingMode.Online"/> in the database as well as here, so
    /// every sitting that existed before this column did reads as what it was. An enum whose zero is
    /// not a member must never be left to EF's scaffolded <c>DEFAULT 0</c> — see the migration.
    /// </remarks>
    public RecruitmentSittingMode Mode { get; set; } = RecruitmentSittingMode.Online;

    /// <summary>What the closed questions scored, marked by the server.</summary>
    public decimal? AutoScore { get; set; }

    /// <summary>What a human awarded for the free-text questions.</summary>
    public decimal? ManualScore { get; set; }

    /// <summary>
    /// The mark that counts, once marking is finished.
    /// </summary>
    /// <remarks>
    /// ⚠ Separate from <see cref="AutoScore"/> on purpose. A paper with free-text questions is not
    /// finished when the machine has done its part, and treating the auto score as final would
    /// publish a mark that is missing the essay.
    /// </remarks>
    public decimal? FinalScore { get; set; }

    public decimal? TotalPoints { get; set; }
    public decimal? ScorePercent { get; set; }
    public bool? Passed { get; set; }

    public Guid? MarkedById { get; set; }
    public DateTime? MarkedAt { get; set; }

    [MaxLength(2000)]
    public string? MarkerNotes { get; set; }

    /// <summary>The ledger row this sitting produced, once finalised.</summary>
    /// <remarks>
    /// ⚠ The link exists so finalising twice cannot write two ledger rows — and so the shortlisting
    /// blend, which reads the ledger, can be traced back to the paper that produced it.
    /// </remarks>
    public Guid? JobApplicantTestResultId { get; set; }

    [ForeignKey(nameof(JobApplicantTestResultId))]
    public virtual JobApplicantTestResult? TestResult { get; set; }

    public virtual ICollection<RecruitmentTestAnswer> Answers { get; set; } = new List<RecruitmentTestAnswer>();
}

/// <summary>One answer in one sitting.</summary>
/// <remarks>
/// ⚠ A multi-select answer is several rows, one per option chosen, exactly as orientation stores
/// them — and the points are carried on the FIRST row only, so summing the column cannot award a
/// question's marks once per option ticked.
/// </remarks>
public class RecruitmentTestAnswer : TenantEntity
{
    public Guid RecruitmentTestSittingId { get; set; }

    [ForeignKey(nameof(RecruitmentTestSittingId))]
    public virtual RecruitmentTestSitting Sitting { get; set; } = null!;

    public Guid RecruitmentTestQuestionId { get; set; }

    [ForeignKey(nameof(RecruitmentTestQuestionId))]
    public virtual RecruitmentTestQuestion Question { get; set; } = null!;

    public Guid? SelectedOptionId { get; set; }

    [ForeignKey(nameof(SelectedOptionId))]
    public virtual RecruitmentTestQuestionOption? SelectedOption { get; set; }

    [MaxLength(4000)]
    public string? FreeTextAnswer { get; set; }

    [MaxLength(100)]
    public string? NumericAnswer { get; set; }

    public bool IsCorrect { get; set; }
    public decimal PointsAwarded { get; set; }

    public DateTime AnsweredAt { get; set; }

    /// <summary>Set by a human when marking a free-text answer.</summary>
    public bool IsManuallyMarked { get; set; }

    [MaxLength(1000)]
    public string? MarkerComment { get; set; }
}
