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
}
