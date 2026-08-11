using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyEscalationCalculationInput(
    QuantitySurveyEscalationComponentType Component,
    decimal Coefficient,
    decimal BaseIndex,
    decimal CurrentIndex);

public sealed record QuantitySurveyEscalationCalculationLineResult(
    QuantitySurveyEscalationComponentType Component,
    decimal Coefficient,
    decimal BaseIndex,
    decimal CurrentIndex,
    decimal IndexRatio,
    decimal WeightedContribution);

public sealed record QuantitySurveyEscalationCalculationResult(
    decimal BaseRate,
    decimal RevisedRate,
    decimal AdjustmentFactor,
    decimal CalculatedFluctuationAmount,
    decimal ReviewerAdjustmentAmount,
    decimal ApprovedImpactAmount,
    IReadOnlyList<QuantitySurveyEscalationCalculationLineResult> Lines);

public static class QuantitySurveyEscalationCalculationRules
{
    public static QuantitySurveyEscalationCalculationResult Calculate(
        decimal baseRate,
        IEnumerable<QuantitySurveyEscalationCalculationInput> source,
        decimal reviewerAdjustmentAmount = 0m)
    {
        if (baseRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(baseRate), "The controlled impact base amount must be greater than zero.");

        var inputs = source.OrderBy(value => value.Component).ToList();
        var required = Enum.GetValues<QuantitySurveyEscalationComponentType>();
        if (inputs.Count != required.Length ||
            inputs.Select(value => value.Component).Distinct().Count() != required.Length ||
            required.Except(inputs.Select(value => value.Component)).Any())
        {
            throw new ArgumentException("Provide each Material, Labour, Plant, and Other calculation component exactly once.", nameof(source));
        }

        if (inputs.Any(value => value.Coefficient < 0m || value.Coefficient > 100m) ||
            inputs.Sum(value => value.Coefficient) != 100m)
        {
            throw new ArgumentException("Calculation coefficients must be between zero and 100 and total exactly 100 percent.", nameof(source));
        }

        if (inputs.Any(value => value.BaseIndex <= 0m || value.CurrentIndex <= 0m))
            throw new ArgumentException("Every base and current price index must be greater than zero.", nameof(source));

        var lines = inputs.Select(value =>
        {
            var ratio = decimal.Round(value.CurrentIndex / value.BaseIndex, 12, MidpointRounding.AwayFromZero);
            var contribution = decimal.Round((value.Coefficient / 100m) * ratio, 12, MidpointRounding.AwayFromZero);
            return new QuantitySurveyEscalationCalculationLineResult(
                value.Component,
                value.Coefficient,
                value.BaseIndex,
                value.CurrentIndex,
                ratio,
                contribution);
        }).ToList();

        var factor = decimal.Round(lines.Sum(value => value.WeightedContribution), 12, MidpointRounding.AwayFromZero);
        if (factor <= 0m)
            throw new ArgumentException("The calculated price-adjustment factor must be greater than zero.", nameof(source));

        var revisedRate = decimal.Round(baseRate * factor, 2, MidpointRounding.AwayFromZero);
        var fluctuation = decimal.Round(revisedRate - baseRate, 2, MidpointRounding.AwayFromZero);
        var approvedImpact = decimal.Round(fluctuation + reviewerAdjustmentAmount, 2, MidpointRounding.AwayFromZero);
        if (baseRate + approvedImpact < 0m)
            throw new ArgumentOutOfRangeException(nameof(reviewerAdjustmentAmount), "The reviewer adjustment cannot reduce the revised amount below zero.");

        return new QuantitySurveyEscalationCalculationResult(
            decimal.Round(baseRate, 2, MidpointRounding.AwayFromZero),
            revisedRate,
            factor,
            fluctuation,
            decimal.Round(reviewerAdjustmentAmount, 2, MidpointRounding.AwayFromZero),
            approvedImpact,
            lines);
    }
}
