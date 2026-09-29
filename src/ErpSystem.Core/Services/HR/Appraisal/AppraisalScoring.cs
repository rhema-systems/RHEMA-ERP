using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Centralized appraisal scoring helpers. The appraisal module enforces a strict 0–100 score
/// invariant: every criterion/overall score is normalized to a 0–100 percentage before it is
/// stored or mapped to a rating. Keep all score→rating logic here so the bands stay in one place.
/// </summary>
public static class AppraisalScoring
{
    /// <summary>Inclusive lower bound of a valid appraisal score.</summary>
    public const decimal MinScore = 0m;

    /// <summary>Inclusive upper bound of a valid appraisal score (the 0–100 invariant).</summary>
    public const decimal MaxScore = 100m;

    /// <summary>True when <paramref name="score"/> is within the 0–100 invariant.</summary>
    public static bool IsValidScore(decimal score) => score >= MinScore && score <= MaxScore;

    /// <summary>Clamps a raw value into the 0–100 range.</summary>
    public static decimal Clamp(decimal score) => Math.Clamp(score, MinScore, MaxScore);

    /// <summary>Maps a 0–100 appraisal overall score to a 5-point performance rating.</summary>
    public static PerformanceRating? MapScoreToRating(decimal? score)
    {
        if (!score.HasValue) return null;
        return score.Value switch
        {
            >= 90 => PerformanceRating.Outstanding,
            >= 75 => PerformanceRating.ExceedsExpectations,
            >= 60 => PerformanceRating.MeetsExpectations,
            >= 40 => PerformanceRating.BelowExpectations,
            _ => PerformanceRating.Unsatisfactory
        };
    }

    // ── The scoring model ────────────────────────────────────────────────────────────
    //
    // Three levels, each normalised to 0–100 before the next one uses it:
    //
    //   1. ITEM      a criterion's achievement, 0–1
    //                  competency  → numericScore / maxScore
    //                  KPI         → KpiAchievementPercent(actual, target, min, max) / 100
    //   2. EVALUATOR a weighted mean of the items *that evaluator scored*, 0–100
    //                  Σ(item × share) / Σ(share)
    //   3. OVERALL   a weighted mean across evaluator ROLES, 0–100
    //                  Σ(roleScore × roleWeight) / Σ(roleWeight),
    //                  with all peers collapsed to one role score first
    //
    // ⚠ Two invariants worth stating, because breaking either produced a live defect:
    //
    //   • The evaluator weight is applied EXACTLY ONCE, at level 3. It used to be baked into
    //     every CriterionScore.WeightedScore *and* applied again during aggregation, so each
    //     role contributed w² instead of w — five evaluators all scoring 80 produced 40.7.
    //   • Peers aggregate to ONE voice. Aggregating over evaluator *records* let a role's
    //     influence scale with its headcount: three peers each carrying the 0.2 peer weight
    //     took peers to 43% of a 1.4 denominator while diluting the manager's 60% to 43%.

    /// <summary>
    /// A criterion's share of its template, 0–100 across the whole form.
    ///
    /// Item weights sum to 100 within a section and section weights sum to 100 across the
    /// template, so the two must be composed — using the item weight alone treats every section
    /// as if it were the only one, which is what made an evaluator's raw total run past 100.
    /// </summary>
    public static decimal CriterionShare(int sectionWeight, int itemWeight)
        => sectionWeight <= 0 ? itemWeight : sectionWeight * itemWeight / 100m;

    /// <summary>
    /// KPI achievement as a percentage of target, clamped to 0–100.
    ///
    /// Hitting target is full marks — there is no extra credit for overshooting, and an actual
    /// beyond an explicit <paramref name="maxValue"/> ceiling counts as attaining the cap. When
    /// a floor is set, achievement is measured across the min→target band rather than from zero.
    /// </summary>
    public static decimal KpiAchievementPercent(
        decimal actualValue, decimal? targetValue, decimal? minValue, decimal? maxValue)
    {
        if (!targetValue.HasValue || targetValue.Value == 0)
            return 0;

        if (maxValue.HasValue && actualValue > maxValue.Value)
            actualValue = maxValue.Value;

        decimal achievementPercent;

        if (minValue.HasValue && targetValue.Value != minValue.Value)
        {
            // Two-segment: at or below min = 0%, at target = 100%.
            if (actualValue <= minValue.Value)
                return 0;

            achievementPercent = (actualValue - minValue.Value) / (targetValue.Value - minValue.Value) * 100;
        }
        else
        {
            achievementPercent = (actualValue / targetValue.Value) * 100;
        }

        return Clamp(achievementPercent);
    }

    /// <summary>
    /// An evaluator's 0–100 score: a weighted mean over the criteria they actually scored.
    ///
    /// The denominator is the shares of the *scored* criteria, not of the whole template, so an
    /// evaluator who is only asked for part of the form is still marked out of 100. That matters
    /// for peers, who are excluded from KPI items unless the cycle opts in — scoring them out of
    /// the full template would silently cap their contribution at the competency share.
    /// </summary>
    /// <param name="scored">(contribution, share) per scored criterion, where contribution = item × share.</param>
    public static decimal EvaluatorScore(IEnumerable<(decimal Contribution, decimal Share)> scored)
    {
        decimal contribution = 0, share = 0;
        foreach (var (c, s) in scored)
        {
            if (s <= 0) continue;
            contribution += c;
            share += s;
        }

        return share > 0 ? Clamp(contribution / share * 100m) : 0m;
    }

    /// <summary>
    /// The overall score: a weighted mean across evaluator roles that actually scored.
    ///
    /// Dividing by the contributing weight rather than by 1.0 means a missing or unweighted
    /// evaluator re-proportions the rest instead of silently dragging the score toward zero.
    ///
    /// Null when no role with a weight scored anything (performance closure A1): nothing was
    /// scored, which is not a score of zero — a zero used to be graded at the bottom band and
    /// pushed to the talent pools as the lowest rating.
    /// </summary>
    /// <param name="roles">(roleScore, roleWeight) — one entry per role, peers already averaged.</param>
    public static decimal? OverallScore(IEnumerable<(decimal Score, decimal Weight)> roles)
    {
        decimal weighted = 0, totalWeight = 0;
        foreach (var (score, weight) in roles)
        {
            if (weight <= 0) continue;
            weighted += score * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? Clamp(weighted / totalWeight) : null;
    }
}
