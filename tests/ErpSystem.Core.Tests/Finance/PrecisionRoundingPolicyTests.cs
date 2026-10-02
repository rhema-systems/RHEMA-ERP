using ErpSystem.Core.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Finance;

public class PrecisionRoundingPolicyTests
{
    [Theory]
    [InlineData("GHS", 2, 12.35)]
    [InlineData("USD", 2, 12.35)]
    [InlineData("JPY", 0, 12)]
    [InlineData("KWD", 3, 12.346)]
    [InlineData("BHD", 3, 12.346)]
    [InlineData("OMR", 3, 12.346)]
    public void CurrencyAmounts_UseIsoMinorUnits(string code, int decimals, decimal expected)
    {
        CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(code).Should().Be(decimals);
        CurrencyMinorUnitPolicy.Round(12.3456m, decimals).Should().Be(expected);
    }

    [Fact]
    public void UnitPriceTimesQuantity_PreservesIntermediatePrecisionUntilCurrencyBoundary()
    {
        PrecisionRoundingPolicy.CalculateMonetaryAmount(3m, 0.333333m, 6, 2)
            .Should().Be(1.00m);
        PrecisionRoundingPolicy.CalculateMonetaryAmount(3m, 0.333333m, 6, 3)
            .Should().Be(1.000m);
    }

    [Theory]
    [InlineData(GovernedRoundingMethod.Nearest, 1.00)]
    [InlineData(GovernedRoundingMethod.Up, 1.05)]
    [InlineData(GovernedRoundingMethod.Down, 1.00)]
    public void IncrementRounding_IsDeterministic(GovernedRoundingMethod method, decimal expected)
    {
        PrecisionRoundingPolicy.RoundToIncrement(1.024m, 0.05m, method).Should().Be(expected);
        PrecisionRoundingPolicy.RoundToIncrement(-1.024m, 0.05m, method).Should().Be(-expected);
    }

    [Fact]
    public void TaxLineAndDocumentScopes_CanProduceDifferentGovernedResults()
    {
        var lineRounded = new[] { 0.03m, 0.03m }
            .Sum(value => PrecisionRoundingPolicy.CalculateTaxAmount(value, 10m, 4, 0.01m, GovernedRoundingMethod.Nearest));
        var documentRounded = PrecisionRoundingPolicy.CalculateTaxAmount(
            0.06m, 10m, 4, 0.01m, GovernedRoundingMethod.Nearest);

        lineRounded.Should().Be(0m);
        documentRounded.Should().Be(0.01m);
    }

    [Fact]
    public void InvoiceRounding_ProducesExplicitDelta()
    {
        PrecisionRoundingPolicy.CalculateInvoiceRoundingDelta(10.02m, 0.05m, GovernedRoundingMethod.Nearest)
            .Should().Be(-0.02m);
    }

    [Fact]
    public void QuantityPrecision_IsIndependentAndEnforcesUomIncrement()
    {
        var act = () => PrecisionRoundingPolicy.ValidateQuantity(1.125m, 3, 0.125m);
        act.Should().NotThrow();

        var invalid = () => PrecisionRoundingPolicy.ValidateQuantity(1.126m, 3, 0.125m);
        invalid.Should().Throw<InvalidOperationException>().WithMessage("*UOM rounding increment*");
    }

    [Fact]
    public void ExchangeRate_RetainsHighPrecisionIndependentOfCurrency()
    {
        PrecisionRoundingPolicy.RoundExchangeRate(0.08001234567m, 10)
            .Should().Be(0.0800123457m);
    }

    [Fact]
    public void SettlementTolerance_IsNotDisplayRounding()
    {
        PrecisionRoundingPolicy.IsWithinSettlementTolerance(0.02m, 100m, 0.01m, 0m)
            .Should().BeFalse();
        PrecisionRoundingPolicy.IsWithinSettlementTolerance(0.02m, 100m, 0.01m, 0.05m)
            .Should().BeTrue();
    }
}
