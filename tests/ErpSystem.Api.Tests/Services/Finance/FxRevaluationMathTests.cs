using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FxRevaluationMathTests
{
    [Theory]
    [InlineData(100, 1_000, 12, 0, 1_200, 200, "Gain")]
    [InlineData(-100, -1_000, 12, 0, -1_200, -200, "Loss")]
    [InlineData(-100, -1_000, 8, 0, -800, 200, "Gain")]
    [InlineData(100, 1_000, 8, 0, 800, -200, "Loss")]
    [InlineData(100, 1_000, 13, 200, 1_300, 100, "Gain")]
    [InlineData(100, 1_000, 12, 200, 1_200, 0, "None")]
    public void Calculate_UsesOneSignedDebitMinusCreditCoordinate(
        decimal signedForeign,
        decimal signedCarrying,
        decimal rate,
        decimal priorAdjustment,
        decimal expectedTarget,
        decimal expectedDelta,
        string expectedType)
    {
        var result = FxRevaluationMath.Calculate(signedForeign, signedCarrying, rate, priorAdjustment);

        result.SignedForeignBalance.Should().Be(signedForeign);
        result.SignedCarryingValue.Should().Be(signedCarrying);
        result.PriorUnreversedAdjustment.Should().Be(priorAdjustment);
        result.AdjustedCarryingValue.Should().Be(signedCarrying + priorAdjustment);
        result.TargetFunctionalValue.Should().Be(expectedTarget);
        result.Delta.Should().Be(expectedDelta);
        result.GainLossType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData(AccountType.Asset, "Debit")]
    [InlineData(AccountType.Expense, "Debit")]
    [InlineData(AccountType.Liability, "Credit")]
    [InlineData(AccountType.Equity, "Credit")]
    [InlineData(AccountType.Revenue, "Credit")]
    public void NormalBalance_IsPresentationOnly(AccountType accountType, string expectedLabel)
    {
        var label = accountType is AccountType.Liability or AccountType.Equity or AccountType.Revenue
            ? "Credit"
            : "Debit";
        var result = FxRevaluationMath.Calculate(-100m, -1_000m, 12m, 0m);

        label.Should().Be(expectedLabel);
        result.Delta.Should().Be(-200m, "core type must not invert signed arithmetic");
    }

    [Fact]
    public void Calculate_RejectsNonPositiveClosingRate()
    {
        var action = () => FxRevaluationMath.Calculate(100m, 1_000m, 0m, 0m);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
