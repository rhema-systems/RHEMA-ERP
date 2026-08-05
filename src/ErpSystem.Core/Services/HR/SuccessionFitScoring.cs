namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Relative weights for the succession fit score. Components with no data are skipped and the
/// remaining weights are renormalised, so a missing signal never drags a candidate down — it
/// simply doesn't contribute. Defaults favour performance and competency fit.
/// </summary>
public sealed record FitScoreWeights(
    double Performance = 0.35,
    double CompetencyFit = 0.30,
    double Potential = 0.20,
    double Tenure = 0.15)
{
    public static readonly FitScoreWeights Default = new();
}

/// <summary>
/// Normalised (0..1) inputs for one candidate. Any component may be null when the underlying
/// data is unavailable (e.g. no appraisal on record, no target-position requirements).
/// </summary>
public sealed record FitScoreInputs(
    double? Performance,
    double? CompetencyFit,
    double? Potential,
    double? Tenure);

public enum FitBand { Low = 0, Emerging = 1, Moderate = 2, Strong = 3 }

public sealed record FitScoreResult(int Score, FitBand Band);

/// <summary>
/// Pure, DB-free succession fit scorer. Combines normalised signals into a single 0–100 score
/// and band. No side effects — fully unit-testable. The search service computes the normalised
/// inputs from appraisal/competency/tenure data and calls <see cref="Score"/>.
/// </summary>
public static class SuccessionFitScoring
{
    public static FitScoreResult Score(FitScoreInputs inputs, FitScoreWeights? weights = null)
    {
        weights ??= FitScoreWeights.Default;

        double weightedSum = 0;
        double weightTotal = 0;

        void Add(double? value, double weight)
        {
            if (value is null) return;
            var v = Math.Clamp(value.Value, 0d, 1d);
            weightedSum += v * weight;
            weightTotal += weight;
        }

        Add(inputs.Performance, weights.Performance);
        Add(inputs.CompetencyFit, weights.CompetencyFit);
        Add(inputs.Potential, weights.Potential);
        Add(inputs.Tenure, weights.Tenure);

        // No signals at all → neutral-unknown score of 0 (caller can treat as "insufficient data").
        var normalized = weightTotal > 0 ? weightedSum / weightTotal : 0d;
        var score = (int)Math.Round(normalized * 100d);

        var band = score >= 75 ? FitBand.Strong
                 : score >= 50 ? FitBand.Moderate
                 : score >= 25 ? FitBand.Emerging
                 : FitBand.Low;

        return new FitScoreResult(score, band);
    }

    /// <summary>
    /// Normalises a raw appraisal overall score to 0..1 by inferring the scale from its magnitude
    /// (≤5 → /5, ≤10 → /10, otherwise → /100). Null when no score is available.
    /// </summary>
    public static double? NormalizePerformance(decimal? overallScore)
    {
        if (overallScore is null) return null;
        var s = (double)overallScore.Value;
        if (s <= 0) return 0d;
        var scale = s <= 5 ? 5d : s <= 10 ? 10d : 100d;
        return Math.Clamp(s / scale, 0d, 1d);
    }

    /// <summary>Normalises years of service to 0..1, saturating at <paramref name="saturateYears"/>.</summary>
    public static double? NormalizeTenure(int? yearsOfService, int saturateYears = 10)
    {
        if (yearsOfService is null) return null;
        if (saturateYears <= 0) return 0d;
        return Math.Clamp((double)yearsOfService.Value / saturateYears, 0d, 1d);
    }

    /// <summary>
    /// Competency/skill fit as a 0..1 fraction: how much of the total required proficiency the
    /// candidate already meets. Levels above the requirement are capped at the requirement (they
    /// don't inflate the score). Null when there are no requirements to measure against.
    /// </summary>
    public static double? CompetencyFitFraction(IEnumerable<(int required, int current)> pairs)
    {
        long totalRequired = 0;
        long totalMet = 0;
        foreach (var (required, current) in pairs)
        {
            if (required <= 0) continue;
            totalRequired += required;
            totalMet += Math.Min(current, required);
        }
        if (totalRequired == 0) return null;
        return Math.Clamp((double)totalMet / totalRequired, 0d, 1d);
    }
}
