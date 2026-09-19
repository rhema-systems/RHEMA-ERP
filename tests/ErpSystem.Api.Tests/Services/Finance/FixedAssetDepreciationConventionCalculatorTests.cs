using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetDepreciationConventionCalculatorTests
{
    [Theory]
    [InlineData(DepreciationConvention.FullMonth, 1, 100)]
    [InlineData(DepreciationConvention.MidMonth, 0.5, 50)]
    [InlineData(DepreciationConvention.ActualDays, 0.39452055, 39.45)]
    [InlineData(DepreciationConvention.HalfYear, 0.20809249, 20.81)]
    public void CommencementMonth_UsesSelectedConvention(
        DepreciationConvention convention,
        decimal expectedFactor,
        decimal expectedAmount)
    {
        var book = StraightLineBook(convention);
        var result = FixedAssetDepreciationCalculator.Calculate(
            book,
            null,
            Timing(new DateTime(2025, 1, 20)));

        result.Convention.Should().Be(convention);
        result.ConventionFactor.Should().Be(expectedFactor);
        result.DepreciationAmount.Should().Be(expectedAmount);
    }

    [Theory]
    [InlineData(DepreciationConvention.FullMonth, 0, 0)]
    [InlineData(DepreciationConvention.MidMonth, 0.5, 50)]
    [InlineData(DepreciationConvention.ActualDays, 0.03287671, 3.29)]
    [InlineData(DepreciationConvention.HalfYear, 6, 600)]
    public void DisposalInCommencementMonth_UsesConventionTerminationRule(
        DepreciationConvention convention,
        decimal expectedFactor,
        decimal expectedAmount)
    {
        var result = FixedAssetDepreciationCalculator.Calculate(
            StraightLineBook(convention),
            null,
            Timing(new DateTime(2025, 1, 20), new DateTime(2025, 1, 20)));

        result.ConventionFactor.Should().Be(expectedFactor);
        result.DepreciationAmount.Should().Be(expectedAmount);
    }

    [Fact]
    public void ActualDays_UsesLeapYearDenominator()
    {
        var result = FixedAssetDepreciationCalculator.Calculate(
            StraightLineBook(DepreciationConvention.ActualDays),
            null,
            new DepreciationTimingContext(
                new DateTime(2024, 2, 1), new DateTime(2024, 2, 29),
                new DateTime(2024, 1, 1), new DateTime(2024, 12, 31),
                new DateTime(2023, 1, 1)));

        result.ConventionFactor.Should().Be(0.95081967m);
        result.DepreciationAmount.Should().Be(95.08m);
    }

    [Fact]
    public void FullMonth_AllocatesCalendarMonthsAcrossIrregularFiscalPeriods()
    {
        var result = FixedAssetDepreciationCalculator.Calculate(
            StraightLineBook(DepreciationConvention.FullMonth),
            null,
            new DepreciationTimingContext(
                new DateTime(2025, 1, 1), new DateTime(2025, 2, 15),
                new DateTime(2025, 1, 1), new DateTime(2025, 12, 31),
                new DateTime(2025, 1, 20)));

        result.ConventionFactor.Should().Be(1.53571429m);
        result.DepreciationAmount.Should().Be(153.57m);
    }

    [Fact]
    public void UnitsOfProduction_TreatsConventionAsInformational()
    {
        var book = StraightLineBook(DepreciationConvention.HalfYear);
        book.DepreciationMethod = DepreciationMethod.UnitsOfProduction;
        book.LifetimeProductionCapacity = 12_000m;
        var result = FixedAssetDepreciationCalculator.Calculate(
            book,
            new FixedAssetProductionUsageDto
            {
                FixedAssetId = Guid.NewGuid(),
                UnitsConsumed = 1_000m,
                EvidenceReference = "METER-25-01"
            },
            Timing(new DateTime(2025, 1, 20)));

        result.DepreciationAmount.Should().Be(100m);
        result.Convention.Should().Be(DepreciationConvention.HalfYear);
        result.ConventionFactor.Should().Be(1m);
        result.ConventionBasis.Should().Be("ProductionUsage");
    }

    [Fact]
    public void HalfYearDisposal_BlocksWhenPriorYearFactorExceedsTarget()
    {
        var act = () => FixedAssetDepreciationCalculator.Calculate(
            StraightLineBook(DepreciationConvention.HalfYear),
            null,
            Timing(new DateTime(2024, 1, 1), new DateTime(2025, 1, 20), priorFactor: 7m));

        act.Should().Throw<InvalidOperationException>().WithMessage("*require reversing excess*");
    }

    private static FixedAssetBookValue StraightLineBook(DepreciationConvention convention) => new()
    {
        AcquisitionCost = 1_200m,
        NetBookValue = 1_200m,
        ResidualValue = 0m,
        UsefulLifeMonths = 12,
        RemainingUsefulLifeMonths = 12,
        DepreciationMethod = DepreciationMethod.StraightLine,
        DepreciationConvention = convention
    };

    private static DepreciationTimingContext Timing(
        DateTime placedInService,
        DateTime? termination = null,
        decimal priorFactor = 0m) => new(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31),
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31),
            placedInService, null, termination, priorFactor);
}
