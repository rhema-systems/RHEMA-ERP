using ErpSystem.Core.Finance;

namespace ErpSystem.Core.Inventory;

/// <summary>Commercial quantity contract owned by the tenant Inventory UOM master.</summary>
public static class CommercialQuantityPolicy
{
    public const int MaximumDecimalPlaces = 6;

    public static void ValidateConfiguration(int decimalPlaces, decimal? roundingIncrement)
    {
        if (decimalPlaces is < 0 or > MaximumDecimalPlaces)
            throw new InvalidOperationException($"Commercial quantity precision must be between 0 and {MaximumDecimalPlaces} decimal places.");
        if (roundingIncrement is null) return;
        if (roundingIncrement <= 0m)
            throw new InvalidOperationException("Commercial quantity rounding increment must be greater than zero.");
        var precisionIncrement = PrecisionRoundingPolicy.Pow10(decimalPlaces);
        if (roundingIncrement < precisionIncrement)
            throw new InvalidOperationException($"Commercial quantity rounding increment cannot be finer than {decimalPlaces}-decimal precision.");
        var scaled = roundingIncrement.Value / precisionIncrement;
        if (scaled != decimal.Truncate(scaled))
            throw new InvalidOperationException($"Commercial quantity rounding increment must align to {decimalPlaces}-decimal precision.");
    }

    public static void Validate(decimal quantity, int decimalPlaces, decimal? roundingIncrement)
    {
        ValidateConfiguration(decimalPlaces, roundingIncrement);
        PrecisionRoundingPolicy.ValidateQuantity(quantity, decimalPlaces, roundingIncrement);
    }
}
