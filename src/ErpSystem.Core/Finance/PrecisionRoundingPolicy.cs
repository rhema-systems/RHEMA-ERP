namespace ErpSystem.Core.Finance;

/// <summary>
/// Rounding directions used by governed Finance calculations. Up and Down are
/// defined by magnitude so credits and reversals remain symmetric.
/// </summary>
public enum GovernedRoundingMethod
{
    Nearest = 0,
    Up = 1,
    Down = 2
}

public enum TaxRoundingScope
{
    Line = 0,
    TaxCodeGroup = 1,
    Document = 2
}

/// <summary>
/// Shared arithmetic contract for non-currency precision domains. Currency
/// amounts still enter the ledger exclusively through CurrencyMinorUnitPolicy.
/// </summary>
public static class PrecisionRoundingPolicy
{
    public const int MaximumUnitPriceDecimalPlaces = 6;
    public const int MinimumExchangeRateDecimalPlaces = 6;
    public const int MaximumExchangeRateDecimalPlaces = 10;
    public const int MaximumPercentageDecimalPlaces = 6;

    public static decimal RoundUnitPrice(decimal value, int decimalPlaces)
    {
        ValidateDecimalPlaces(decimalPlaces, 0, MaximumUnitPriceDecimalPlaces, "Unit-price/cost");
        return decimal.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundExchangeRate(decimal value, int decimalPlaces)
    {
        if (value <= 0m)
            throw new InvalidOperationException("Exchange rates must be greater than zero.");

        ValidateDecimalPlaces(
            decimalPlaces,
            MinimumExchangeRateDecimalPlaces,
            MaximumExchangeRateDecimalPlaces,
            "Exchange-rate");
        return decimal.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundPercentage(decimal value, int decimalPlaces)
    {
        ValidateDecimalPlaces(decimalPlaces, 0, MaximumPercentageDecimalPlaces, "Percentage");
        return decimal.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundToIncrement(
        decimal value,
        decimal increment,
        GovernedRoundingMethod method)
    {
        if (increment <= 0m)
            throw new InvalidOperationException("Rounding increment must be greater than zero.");

        var magnitude = decimal.Abs(value) / increment;
        var roundedMagnitude = method switch
        {
            GovernedRoundingMethod.Nearest => decimal.Round(magnitude, 0, MidpointRounding.AwayFromZero),
            GovernedRoundingMethod.Up => decimal.Ceiling(magnitude),
            GovernedRoundingMethod.Down => decimal.Floor(magnitude),
            _ => throw new InvalidOperationException($"Unsupported rounding method '{method}'.")
        };

        return decimal.Sign(value) * roundedMagnitude * increment;
    }

    public static decimal CalculateMonetaryAmount(
        decimal quantity,
        decimal unitPrice,
        int unitPriceDecimalPlaces,
        int currencyDecimalPlaces)
    {
        var governedUnitPrice = RoundUnitPrice(unitPrice, unitPriceDecimalPlaces);
        var unroundedAmount = quantity * governedUnitPrice;
        return CurrencyMinorUnitPolicy.Round(unroundedAmount, currencyDecimalPlaces);
    }

    public static decimal CalculateTaxAmount(
        decimal taxableAmount,
        decimal percentage,
        int percentageDecimalPlaces,
        decimal monetaryIncrement,
        GovernedRoundingMethod method)
    {
        var governedPercentage = RoundPercentage(percentage, percentageDecimalPlaces);
        return RoundToIncrement(taxableAmount * governedPercentage / 100m, monetaryIncrement, method);
    }

    public static decimal CalculateInvoiceRoundingDelta(
        decimal documentTotal,
        decimal increment,
        GovernedRoundingMethod method) =>
        RoundToIncrement(documentTotal, increment, method) - documentTotal;

    public static void ValidateQuantity(decimal quantity, int decimalPlaces, decimal? roundingIncrement)
    {
        ValidateDecimalPlaces(decimalPlaces, 0, MaximumUnitPriceDecimalPlaces, "Quantity");

        var decimalIncrement = Pow10(decimalPlaces);
        var effectiveIncrement = roundingIncrement ?? decimalIncrement;
        if (effectiveIncrement <= 0m)
            throw new InvalidOperationException("UOM rounding increment must be greater than zero.");
        if (effectiveIncrement < decimalIncrement)
            throw new InvalidOperationException(
                $"UOM rounding increment cannot be finer than its {decimalPlaces}-decimal precision.");

        var units = quantity / effectiveIncrement;
        if (units != decimal.Truncate(units))
            throw new InvalidOperationException(
                $"Quantity must be a whole multiple of the UOM rounding increment {effectiveIncrement}.");
    }

    public static bool IsWithinSettlementTolerance(
        decimal difference,
        decimal documentAmount,
        decimal amountTolerance,
        decimal percentageTolerance)
    {
        if (amountTolerance < 0m || percentageTolerance is < 0m or > 100m)
            throw new InvalidOperationException("Settlement tolerances must be non-negative and percentage tolerance cannot exceed 100.");

        var percentageAmount = decimal.Abs(documentAmount) * percentageTolerance / 100m;
        return decimal.Abs(difference) <= decimal.Max(amountTolerance, percentageAmount);
    }

    public static decimal Pow10(int decimalPlaces)
    {
        ValidateDecimalPlaces(decimalPlaces, 0, MaximumExchangeRateDecimalPlaces, "Precision");
        var result = 1m;
        for (var index = 0; index < decimalPlaces; index++)
            result /= 10m;
        return result;
    }

    private static void ValidateDecimalPlaces(int value, int minimum, int maximum, string label)
    {
        if (value < minimum || value > maximum)
            throw new InvalidOperationException($"{label} precision must be between {minimum} and {maximum} decimal places.");
    }
}
