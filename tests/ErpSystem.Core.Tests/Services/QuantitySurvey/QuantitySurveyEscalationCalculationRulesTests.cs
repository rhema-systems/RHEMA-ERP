using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationCalculationRulesTests
{
    [Fact]
    public void Calculate_uses_all_controlled_components_and_keeps_adjustment_separate()
    {
        var result = QuantitySurveyEscalationCalculationRules.Calculate(
            1_000_000m,
            Inputs((120m, 132m), (150m, 165m), (110m, 121m), (100m, 100m)),
            -5_000m);

        result.AdjustmentFactor.Should().Be(1.09m);
        result.RevisedRate.Should().Be(1_090_000m);
        result.CalculatedFluctuationAmount.Should().Be(90_000m);
        result.ReviewerAdjustmentAmount.Should().Be(-5_000m);
        result.ApprovedImpactAmount.Should().Be(85_000m);
        result.Lines.Should().HaveCount(4);
    }

    [Fact]
    public void Calculate_rejects_missing_or_duplicate_components()
    {
        var invalid = Inputs((100m, 110m), (100m, 110m), (100m, 110m), (100m, 110m)).ToList();
        invalid[3] = invalid[3] with { Component = QuantitySurveyEscalationComponentType.Plant };

        var action = () => QuantitySurveyEscalationCalculationRules.Calculate(100m, invalid);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*exactly once*");
    }

    [Fact]
    public void Calculate_rejects_coefficients_that_do_not_total_one_hundred()
    {
        var invalid = Inputs((100m, 110m), (100m, 110m), (100m, 110m), (100m, 110m))
            .Select(value => value with { Coefficient = 20m });

        var action = () => QuantitySurveyEscalationCalculationRules.Calculate(100m, invalid);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*total exactly 100 percent*");
    }

    [Fact]
    public void Calculate_rejects_non_positive_indices_and_over_reduction()
    {
        var invalidIndex = Inputs((0m, 110m), (100m, 110m), (100m, 110m), (100m, 110m));
        var valid = Inputs((100m, 50m), (100m, 50m), (100m, 50m), (100m, 50m));

        Action invalidIndexAction = () =>
            QuantitySurveyEscalationCalculationRules.Calculate(100m, invalidIndex);
        Action overReductionAction = () =>
            QuantitySurveyEscalationCalculationRules.Calculate(100m, valid, -60m);

        invalidIndexAction.Should().Throw<ArgumentException>();
        overReductionAction.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static IEnumerable<QuantitySurveyEscalationCalculationInput> Inputs(
        (decimal Base, decimal Current) material,
        (decimal Base, decimal Current) labour,
        (decimal Base, decimal Current) plant,
        (decimal Base, decimal Current) other)
    {
        yield return new(QuantitySurveyEscalationComponentType.Material, 40m, material.Base, material.Current);
        yield return new(QuantitySurveyEscalationComponentType.Labour, 30m, labour.Base, labour.Current);
        yield return new(QuantitySurveyEscalationComponentType.Plant, 20m, plant.Base, plant.Current);
        yield return new(QuantitySurveyEscalationComponentType.Other, 10m, other.Base, other.Current);
    }
}
