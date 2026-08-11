using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyCostReconciliationCalculatorTests
{
    [Fact]
    public void Balancing_line_closes_every_commercial_total_and_preserves_markup_residual()
    {
        var expected = new QuantitySurveyCostReconciliationAmounts(125m, 140m, 80m, 55m, 60m, 150m);
        var mapped = new[] { Line(100m, 110m, 70m, 50m, 45m, 120m) };

        var balancing = QuantitySurveyCostReconciliationCalculator.BuildBalancingLine(2, expected, mapped);
        var complete = mapped.Append(balancing).ToList();

        balancing.EstimateAmount.Should().Be(25m);
        balancing.ApprovedBudgetAmount.Should().Be(30m);
        balancing.CommittedAmount.Should().Be(10m);
        balancing.CertifiedAmount.Should().Be(5m);
        balancing.ActualAmount.Should().Be(15m);
        balancing.ForecastAmount.Should().Be(30m);
        balancing.BudgetVarianceAmount.Should().Be(5m);
        balancing.ForecastVarianceAmount.Should().Be(0m);
        QuantitySurveyCostReconciliationCalculator.HasCommercialAmount(balancing).Should().BeTrue();
        var action = () => QuantitySurveyCostReconciliationCalculator.EnsureBalanced(expected, complete);
        action.Should().NotThrow();
    }

    [Fact]
    public void Exact_direct_mapping_does_not_require_an_unallocated_row()
    {
        var expected = new QuantitySurveyCostReconciliationAmounts(100m, 110m, 70m, 50m, 45m, 120m);
        var balancing = QuantitySurveyCostReconciliationCalculator.BuildBalancingLine(
            2,
            expected,
            new[] { Line(100m, 110m, 70m, 50m, 45m, 120m) });

        QuantitySurveyCostReconciliationCalculator.HasCommercialAmount(balancing).Should().BeFalse();
    }

    [Fact]
    public void Over_attribution_remains_visible_as_a_negative_residual()
    {
        var expected = new QuantitySurveyCostReconciliationAmounts(100m, 100m, 100m, 100m, 100m, 100m);
        var balancing = QuantitySurveyCostReconciliationCalculator.BuildBalancingLine(
            2,
            expected,
            new[] { Line(110m, 120m, 130m, 140m, 150m, 160m) });

        balancing.EstimateAmount.Should().Be(-10m);
        balancing.ApprovedBudgetAmount.Should().Be(-20m);
        balancing.CommittedAmount.Should().Be(-30m);
        balancing.CertifiedAmount.Should().Be(-40m);
        balancing.ActualAmount.Should().Be(-50m);
        balancing.ForecastAmount.Should().Be(-60m);
    }

    private static QuantitySurveyCostReconciliationLineDto Line(
        decimal estimate,
        decimal budget,
        decimal committed,
        decimal certified,
        decimal actual,
        decimal forecast)
        => new()
        {
            EstimateAmount = estimate,
            ApprovedBudgetAmount = budget,
            CommittedAmount = committed,
            CertifiedAmount = certified,
            ActualAmount = actual,
            ForecastAmount = forecast
        };
}
