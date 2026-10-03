namespace ErpSystem.Api.Services.Finance;

/// <summary>Allocate a rounded nonnegative total without pushing a rounding loss onto the last item.</summary>
public static class MonetaryAllocation
{
    public static decimal[] Allocate(
        IReadOnlyList<decimal> amounts,
        decimal target,
        int currencyDecimalPlaces = 2)
    {
        CurrencyMinorUnitPolicy.Validate("allocation currency", currencyDecimalPlaces);
        if (target < 0m
            || target != CurrencyMinorUnitPolicy.Round(target, currencyDecimalPlaces)
            || amounts.Any(value => value < 0m))
            throw new InvalidOperationException(
                "Allocation requires nonnegative values and a total rounded to the currency minor unit.");
        var result = new decimal[amounts.Count];
        if (target == 0m) return result;
        var total = amounts.Sum();
        if (total <= 0m) throw new InvalidOperationException("A positive allocation requires a positive basis.");
        var scale = DecimalScale(currencyDecimalPlaces);
        var fractions = new decimal[amounts.Count];
        for (var index = 0; index < amounts.Count; index++)
        {
            var scaledUnits = target * scale * (amounts[index] / total);
            var floor = decimal.Floor(scaledUnits);
            result[index] = floor / scale;
            fractions[index] = scaledUnits - floor;
        }
        var remaining = checked((int)((target - result.Sum()) * scale));
        // Stable input order breaks equal-remainder ties; zero-basis rows never receive money.
        foreach (var index in Enumerable.Range(0, amounts.Count).Where(index => amounts[index] > 0m)
                     .OrderByDescending(index => fractions[index]).ThenBy(index => index).Take(remaining))
            result[index] += 1m / scale;
        if (result.Sum() != target)
            throw new InvalidOperationException("The monetary allocation could not preserve its total.");
        return result;
    }

    private static decimal DecimalScale(int decimalPlaces) => decimalPlaces switch
    {
        0 => 1m,
        1 => 10m,
        2 => 100m,
        3 => 1_000m,
        4 => 10_000m,
        _ => throw new InvalidOperationException("Currency decimal places must be between 0 and 4.")
    };
}
