using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyCostDashboardCalculatorTests
{
    [Fact]
    public void Calculate_UsesActiveForecastAndEacWithoutDoubleCountingVariations()
    {
        var result = QuantitySurveyCostDashboardCalculator.Calculate(
            packageForecast: 900m,
            actualCost: 550m,
            activeForecastCost: 1_050m,
            activeEstimateAtCompletion: 1_125m,
            approvedBudget: 1_200m);

        result.ForecastCost.Should().Be(1_050m);
        result.FinalProjectedCost.Should().Be(1_125m);
        result.CostToComplete.Should().Be(575m);
        result.BudgetVariance.Should().Be(75m);
    }

    [Fact]
    public void Calculate_FallsBackToExistingPackageForecast()
    {
        var result = QuantitySurveyCostDashboardCalculator.Calculate(800m, 300m, null, null, 750m);

        result.ForecastCost.Should().Be(800m);
        result.FinalProjectedCost.Should().Be(800m);
        result.CostToComplete.Should().Be(500m);
        result.BudgetVariance.Should().Be(-50m);
    }

    [Fact]
    public void Calculate_RejectsNegativeOwnerValues()
    {
        var action = () => QuantitySurveyCostDashboardCalculator.Calculate(1m, -1m, null, null, 1m);
        action.Should().Throw<InvalidOperationException>();
    }
}
