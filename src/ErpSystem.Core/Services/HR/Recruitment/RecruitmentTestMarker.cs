using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// Marks a sitting. On its own, so the rule can be read in one place and tested without a database.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane E5.</b> This is <c>EmployeeOrientationService.SubmitAssessmentAsync</c>'s
/// grading, carried across deliberately (decision D-2) — including the fix it had to learn the hard
/// way, which is stated again below because it is the whole reason this is a separate class rather
/// than a loop inside the service.</para>
///
/// <para>⚠ <b>THE DENOMINATOR IS EVERY GRADABLE QUESTION ON THE PAPER</b>, not the ones that came
/// back in the payload. Orientation accumulated it inside the answer loop, so an unanswered question
/// left the paper entirely: answering one of ten correctly and omitting the rest scored <b>100%</b>,
/// passed, completed the enrolment and issued a certificate. A candidate who skips is not a
/// candidate who is right.</para>
///
/// <para>⚠ <b>Multi-select is EXACT SET EQUALITY.</b> Every correct option and no incorrect one.
/// Partial credit for a partly-right answer is a policy somebody must choose deliberately; awarding
/// it silently would mean ticking every box scored well.</para>
/// </remarks>
public static class RecruitmentTestMarker
{
    /// <summary>A question the machine can mark. FreeText is the only kind that is not.</summary>
    public static bool IsAutoGradable(RecruitmentQuestionType type) =>
        type != RecruitmentQuestionType.FreeText;

    /// <summary>
    /// Marks one submission.
    /// </summary>
    /// <param name="questions">
    /// ⚠ Every question ON THE PAPER, not the answered ones. Passing the answered subset is the
    /// orientation bug, reintroduced.
    /// </param>
    /// <param name="submitted">What the candidate sent, keyed by question.</param>
    public static MarkingResult Mark(
        IReadOnlyList<RecruitmentTestQuestion> questions,
        IReadOnlyDictionary<Guid, SubmittedAnswer> submitted)
    {
        var gradable = questions.Where(q => IsAutoGradable(q.QuestionType)).ToList();
        var freeText = questions.Where(q => !IsAutoGradable(q.QuestionType)).ToList();

        // ⚠ Both totals come from the PAPER. `TotalPoints` is what a perfect script scores;
        // `AutoMarkablePoints` is the part the machine can settle now.
        var totalPoints = questions.Sum(q => q.Points);
        var autoMarkablePoints = gradable.Sum(q => q.Points);

        decimal awarded = 0m;
        var correctCount = 0;
        var marks = new List<AnswerMark>();

        foreach (var q in gradable)
        {
            submitted.TryGetValue(q.Id, out var answer);
            var isCorrect = IsCorrect(q, answer);

            if (isCorrect)
            {
                correctCount++;
                awarded += q.Points;
            }

            // ⚠ One mark row per option chosen, and the points ride on the FIRST only — otherwise
            // summing the column awards a multi-select question's marks once per box ticked.
            var chosen = answer?.SelectedOptionIds?.Distinct().ToList() ?? new List<Guid>();
            if (chosen.Count == 0)
            {
                marks.Add(new AnswerMark(q.Id, null, answer?.FreeTextAnswer, answer?.NumericAnswer,
                    isCorrect, isCorrect ? q.Points : 0m));
            }
            else
            {
                var first = true;
                foreach (var optionId in chosen)
                {
                    marks.Add(new AnswerMark(q.Id, optionId, null, null, isCorrect,
                        first && isCorrect ? q.Points : 0m));
                    first = false;
                }
            }
        }

        // Free text is stored and left for a human. Never scored zero here: a zero is a judgement,
        // and nobody has made one yet.
        foreach (var q in freeText)
        {
            submitted.TryGetValue(q.Id, out var answer);
            marks.Add(new AnswerMark(q.Id, null, answer?.FreeTextAnswer, null, false, 0m));
        }

        return new MarkingResult(
            AwardedPoints: awarded,
            AutoMarkablePoints: autoMarkablePoints,
            TotalPoints: totalPoints,
            CorrectCount: correctCount,
            GradableCount: gradable.Count,
            AwaitingManualMarking: freeText.Count > 0,
            Marks: marks);
    }

    /// <remarks>
    /// ⚠ An empty answer is never correct, however the question is shaped. Without that guard a
    /// question whose options are all wrong would mark a blank as right, because "the set of chosen
    /// options equals the set of correct options" is trivially true when both are empty.
    /// </remarks>
    private static bool IsCorrect(RecruitmentTestQuestion q, SubmittedAnswer? answer)
    {
        if (answer is null) return false;

        if (q.QuestionType == RecruitmentQuestionType.Numeric)
        {
            if (string.IsNullOrWhiteSpace(answer.NumericAnswer) ||
                string.IsNullOrWhiteSpace(q.ExpectedAnswer)) return false;

            // Compared as NUMBERS, not strings: "7.0" and "7" are the same answer, and a candidate
            // should not lose a mark to a trailing zero.
            return decimal.TryParse(answer.NumericAnswer, out var given)
                && decimal.TryParse(q.ExpectedAnswer, out var expected)
                && given == expected;
        }

        var correctIds = q.Options.Where(o => o.IsCorrect && !o.IsDeleted)
            .Select(o => o.Id).OrderBy(x => x).ToList();
        var chosenIds = (answer.SelectedOptionIds ?? Array.Empty<Guid>())
            .Distinct().OrderBy(x => x).ToList();

        if (chosenIds.Count == 0) return false;
        return correctIds.SequenceEqual(chosenIds);
    }
}

/// <summary>What the candidate sent for one question.</summary>
public sealed record SubmittedAnswer(
    Guid QuestionId,
    IReadOnlyList<Guid>? SelectedOptionIds,
    string? FreeTextAnswer,
    string? NumericAnswer);

/// <summary>One question's verdict, ready to become <see cref="RecruitmentTestAnswer"/> rows.</summary>
public sealed record AnswerMark(
    Guid QuestionId,
    Guid? SelectedOptionId,
    string? FreeTextAnswer,
    string? NumericAnswer,
    bool IsCorrect,
    decimal PointsAwarded);

/// <summary>What the machine could settle, and what it could not.</summary>
/// <param name="AutoMarkablePoints">
/// The part of the paper the machine can mark. ⚠ The percentage a candidate is told at submit is
/// out of THIS, not <paramref name="TotalPoints"/> — a script with an unmarked essay is not a
/// script scored zero on the essay.
/// </param>
/// <param name="AwaitingManualMarking">
/// True when the paper has free-text questions, so the sitting is <c>AwaitingMarking</c> rather than
/// <c>Marked</c> and no ledger row is written yet.
/// </param>
public sealed record MarkingResult(
    decimal AwardedPoints,
    decimal AutoMarkablePoints,
    decimal TotalPoints,
    int CorrectCount,
    int GradableCount,
    bool AwaitingManualMarking,
    IReadOnlyList<AnswerMark> Marks)
{
    /// <summary>
    /// The percentage of the auto-markable part that was correct.
    /// </summary>
    /// <remarks>
    /// ⚠ Zero when there is nothing to mark, NOT 100. A paper of nothing but essays has no machine
    /// score, and reporting a perfect one would be the "empty criterion scores full marks" fault
    /// this module removed from shortlisting, arriving in a different room.
    /// </remarks>
    public decimal AutoScorePercent =>
        AutoMarkablePoints > 0 ? Math.Round(AwardedPoints / AutoMarkablePoints * 100m, 2) : 0m;
}
