using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Authors a test paper, puts it in front of candidates, receives what they sat, and marks it.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane E.</b> <c>JobVacancy.TestScoreWeight</c> has always blended a test score
/// into the shortlisting score, and until now there was nothing to blend: the module had a result
/// LEDGER (<c>JobApplicantTestResult</c> — a name, a date, a venue, a number somebody typed) and no
/// way to produce a result. § 3 defect 8.</para>
///
/// <para><b>Three audiences, one service, and the split matters.</b> The HR methods take no caller
/// identity because the controller's role gate is the fence. The candidate methods all take
/// <c>userId</c> and <c>tenantId</c> explicitly and resolve ownership through
/// <c>JobCandidate.UserId</c> — a candidate may only ever reach a sitting that belongs to an
/// application of theirs, and a guessed id is a miss, not a disclosure. The sweep method takes a
/// tenant because it runs with no user at all.</para>
///
/// <para>⚠ <b>Nothing a candidate can call returns <c>IsCorrect</c> or <c>ExpectedAnswer</c>.</b>
/// That is enforced by type: the candidate reads return <see cref="CandidateSittingDto"/>, whose
/// question and option types do not carry those fields at all. See the DTO file's header.</para>
/// </remarks>
public interface IRecruitmentTestService
{
    // ── E2 — authoring ──────────────────────────────────────────────────────────

    Task<IEnumerable<RecruitmentTestDto>> GetTestsAsync(
        bool? activeOnly = null, CancellationToken cancellationToken = default);

    Task<RecruitmentTestDto> GetTestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecruitmentTestDto> CreateTestAsync(
        CreateRecruitmentTestDto dto, CancellationToken cancellationToken = default);

    /// <summary>⚠ Refused once anybody has sat the paper. See <see cref="SetTestActiveAsync"/>.</summary>
    Task<RecruitmentTestDto> UpdateTestAsync(
        UpdateRecruitmentTestDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteTestAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or retires a paper.
    /// </summary>
    /// <remarks>
    /// ⚠ Activation is the gate, and it is where the paper is CHECKED: a test with no questions, a
    /// closed question with no correct option, a numeric question with no expected answer or a
    /// true/false question without exactly two options are all refused here, naming the question.
    /// Letting an unmarkable paper go live is how a candidate sits twenty minutes of work that can
    /// only ever score zero.
    /// </remarks>
    Task<RecruitmentTestDto> SetTestActiveAsync(
        Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<RecruitmentTestSectionDto> AddSectionAsync(
        CreateRecruitmentTestSectionDto dto, CancellationToken cancellationToken = default);

    Task<RecruitmentTestSectionDto> UpdateSectionAsync(
        UpdateRecruitmentTestSectionDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecruitmentTestQuestionDto> AddQuestionAsync(
        CreateRecruitmentTestQuestionDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// ⚠ The options are a REPLACE-SET: the payload carries the whole list and a row left out is
    /// removed, exactly as a vacancy's shortlisting criterion values do.
    /// </summary>
    Task<RecruitmentTestQuestionDto> UpdateQuestionAsync(
        UpdateRecruitmentTestQuestionDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteQuestionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The paper exactly as a candidate would see it — through the same projection they are served.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NOT a separate rendering. A preview built from the authoring DTO would show
    /// the author a page that proves nothing about what the candidate gets, and the one thing a
    /// preview exists to prove is that the answers are not visible.
    /// </remarks>
    Task<CandidateSittingDto> PreviewTestAsync(Guid testId, CancellationToken cancellationToken = default);

    // ── E3 — assignment and invitation ──────────────────────────────────────────

    Task<RecruitmentTestAssignmentDto> AssignAsync(
        CreateRecruitmentTestAssignmentDto dto, CancellationToken cancellationToken = default);

    Task<IEnumerable<RecruitmentTestAssignmentDto>> GetAssignmentsAsync(
        Guid? testId = null, Guid? vacancyId = null, Guid? applicationId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails everybody the assignment reaches, and records that it happened.
    /// </summary>
    /// <returns>How many invitations were sent.</returns>
    /// <remarks>
    /// ⚠ A vacancy-wide assignment reaches every LIVE application — not the withdrawn, rejected or
    /// already-hired ones. Inviting a rejected candidate to sit an aptitude test is the kind of
    /// mistake that ends up on social media.
    /// </remarks>
    Task<int> InviteAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>⚠ Decision Q4 — a re-sit is granted with a reason on record, or not at all.</summary>
    Task<RecruitmentTestAssignmentDto> GrantExtraAttemptAsync(
        GrantExtraAttemptDto dto, CancellationToken cancellationToken = default);

    // ── E5 — HR's side of a sitting ─────────────────────────────────────────────

    Task<IEnumerable<RecruitmentTestSittingDto>> GetSittingsAsync(
        Guid? testId = null, Guid? assignmentId = null, Guid? applicationId = null,
        Guid? vacancyId = null, RecruitmentSittingStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<RecruitmentTestSittingDto> GetSittingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Awards marks for one free-text answer.</summary>
    Task<RecruitmentTestSittingDto> MarkAnswerAsync(
        MarkFreeTextAnswerDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the marking, writes the ledger row, and re-scores the application.
    /// </summary>
    /// <remarks>
    /// <para>⚠ This is the point of the whole lane. It writes a <c>JobApplicantTestResult</c> and
    /// then calls <c>EvaluateApplicationScoreAsync</c>, which is where the <c>TestScoreWeight</c>
    /// blend that has never had an input finally runs.</para>
    ///
    /// <para>⚠ Finalising twice cannot write two ledger rows — the sitting carries the id of the
    /// row it produced, and a second call updates it.</para>
    /// </remarks>
    Task<RecruitmentTestSittingDto> FinaliseSittingAsync(
        FinaliseSittingDto dto, CancellationToken cancellationToken = default);

    // ── E6 — offline ───────────────────────────────────────────────────────────

    /// <summary>
    /// Everybody an assignment reaches, with the attempts they have used and whether a paper
    /// sitting can be recorded for them — and, when not, why.
    /// </summary>
    Task<IEnumerable<RecruitmentTestAssignmentCandidateDto>> GetAssignmentCandidatesAsync(
        Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a script sat on the printed paper: what was ticked, and the written marks.
    /// </summary>
    /// <remarks>
    /// <para>⚠ The closed questions are marked by the SAME marker an online sitting uses, against the
    /// whole paper, so a paper script and an online one cannot be marked differently. The sitting is
    /// finalised in the same call — ledger row written, application re-scored — because it is
    /// entered from a script that has already been marked.</para>
    ///
    /// <para>⚠ It counts as an attempt, it is refused over a running online attempt, and it must have
    /// been sat inside the assignment's window — judged by the date it was SAT, not the day it is
    /// typed in.</para>
    /// </remarks>
    Task<RecruitmentTestSittingDto> RecordPaperSittingAsync(
        RecordPaperSittingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every sitting whose clock ran out while nobody was looking.
    /// </summary>
    /// <remarks>
    /// ⚠ Runs on the recruitment lifecycle sweep, which is the module's one date-driven job. A
    /// candidate who closes the tab and never comes back leaves a sitting <c>InProgress</c> for
    /// ever; without this it stays in HR's queue as work outstanding, and the attempt is never
    /// released either — so the candidate cannot legitimately re-sit.
    /// </remarks>
    Task<int> ExpireOverdueSittingsAsync(
        Guid tenantId, DateTime now, Guid? actingUserId, CancellationToken cancellationToken = default);

    // ── E4 — the candidate's side ───────────────────────────────────────────────

    Task<IEnumerable<CandidateAssessmentSummaryDto>> GetMyAssessmentsAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an attempt, or hands back the one already open.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>MustSubmitBy</c> is fixed HERE, once, from the duration. Recomputing it at submit from
    /// "now minus duration" would let a candidate close the tab and come back tomorrow with a full
    /// clock — which is exactly what "resume on refresh" would otherwise mean.
    /// </remarks>
    Task<CandidateSittingDto> StartSittingAsync(
        Guid userId, Guid tenantId, Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Resumes an attempt: the paper, plus everything already answered.</summary>
    Task<CandidateSittingDto> GetMySittingAsync(
        Guid userId, Guid tenantId, Guid sittingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores what has been answered so far, unmarked.
    /// </summary>
    /// <remarks>
    /// ⚠ Server-side, not <c>localStorage</c>. A candidate whose browser crashes twenty minutes into
    /// a timed test has lost the time either way; they should not also have lost the answers. It is
    /// also what makes a timed-out sitting markable at all — see the submit path.
    /// </remarks>
    Task<CandidateSittingDto> SaveProgressAsync(
        Guid userId, Guid tenantId, SubmitSittingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits, marks the closed questions, and says what happens next.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>The timer is enforced HERE.</b> A countdown that exists only in the browser is a
    /// suggestion; the server holds <c>MustSubmitBy</c> and compares against it.</para>
    ///
    /// <para>⚠ Past the deadline the late payload is IGNORED and the sitting is marked on what was
    /// saved before it, status <c>Expired</c>. It does not throw: refusing outright would lose work
    /// the candidate did in time, and accepting the late answers would make the clock decorative.
    /// A short grace covers clock skew and the round trip.</para>
    /// </remarks>
    Task<CandidateSittingResultDto> SubmitSittingAsync(
        Guid userId, Guid tenantId, SubmitSittingDto dto, CancellationToken cancellationToken = default);
}
