namespace ErpSystem.Api.Services.Finance.MultiCurrency;

/// <summary>
/// One invariant coordinate for every core account type: debit minus credit.
/// Normal balance is descriptive evidence and never changes this formula.
/// </summary>
public static class FxRevaluationMath
{
    public static FxRevaluationCalculation Calculate(
        decimal signedForeignBalance,
        decimal signedCarryingValue,
        decimal closingRate,
        decimal priorUnreversedAdjustment)
    {
        if (closingRate <= 0m) throw new ArgumentOutOfRangeException(nameof(closingRate));
        var target = Round(signedForeignBalance * closingRate);
        var adjustedCarrying = Round(signedCarryingValue + priorUnreversedAdjustment);
        var delta = Round(target - adjustedCarrying);
        return new FxRevaluationCalculation(
            Round(signedForeignBalance),
            Round(signedCarryingValue),
            Round(priorUnreversedAdjustment),
            adjustedCarrying,
            target,
            delta,
            delta > 0m ? "Gain" : delta < 0m ? "Loss" : "None");
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record FxRevaluationCalculation(
    decimal SignedForeignBalance,
    decimal SignedCarryingValue,
    decimal PriorUnreversedAdjustment,
    decimal AdjustedCarryingValue,
    decimal TargetFunctionalValue,
    decimal Delta,
    string GainLossType);
