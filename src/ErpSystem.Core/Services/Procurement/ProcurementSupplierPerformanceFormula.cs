using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementSupplierPerformanceFormula
{
    public static ProcurementSupplierPerformanceFormulaResult Calculate(
        IReadOnlyCollection<ProcurementSupplierPerformanceMeasureDto> measures,
        decimal minimumCoveragePercent)
    {
        if (measures.Count == 0)
            throw new ArgumentException("At least one performance measure is required.",
                nameof(measures));
        if (measures.Any(item => item.WeightPercent <= 0 || item.WeightPercent > 100 ||
                                 item.Score is < 0 or > 100))
            throw new ArgumentOutOfRangeException(nameof(measures),
                "Performance scores and weights must be valid percentages.");
        if (measures.Sum(item => item.WeightPercent) != 100)
            throw new ArgumentException("Performance measure weights must total 100 percent.",
                nameof(measures));
        if (minimumCoveragePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumCoveragePercent));

        var measured = measures.Where(item => item.Score.HasValue).ToList();
        var coverage = measured.Sum(item => item.WeightPercent);
        var overall = coverage <= 0
            ? (decimal?)null
            : decimal.Round(measured.Sum(item => item.Score!.Value * item.WeightPercent) /
                            coverage, 2, MidpointRounding.AwayFromZero);

        foreach (var measure in measures)
        {
            if (!measure.Score.HasValue || coverage <= 0)
            {
                measure.AppliedWeightPercent = null;
                measure.WeightedContribution = null;
                continue;
            }
            measure.AppliedWeightPercent = decimal.Round(
                measure.WeightPercent / coverage * 100, 2,
                MidpointRounding.AwayFromZero);
            measure.WeightedContribution = decimal.Round(
                measure.Score.Value * measure.WeightPercent / coverage, 2,
                MidpointRounding.AwayFromZero);
        }

        return new ProcurementSupplierPerformanceFormulaResult(
            overall,
            decimal.Round(coverage, 2, MidpointRounding.AwayFromZero),
            overall.HasValue && coverage >= minimumCoveragePercent);
    }

    public static decimal Clamp(decimal value) => Math.Min(100, Math.Max(0, value));
}

public sealed record ProcurementSupplierPerformanceFormulaResult(
    decimal? OverallScore,
    decimal CoveragePercent,
    bool DataComplete);
