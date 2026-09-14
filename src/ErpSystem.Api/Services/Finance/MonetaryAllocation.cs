namespace ErpSystem.Api.Services.Finance;

/// <summary>Allocate a rounded nonnegative total without pushing a rounding loss onto the last item.</summary>
public static class MonetaryAllocation
{
    public static decimal[] Allocate(IReadOnlyList<decimal> amounts, decimal target)
    {
        if (target < 0m || target != decimal.Round(target, 2) || amounts.Any(value => value < 0m))
            throw new InvalidOperationException("Allocation requires nonnegative values and a total rounded to cents.");
        var result = new decimal[amounts.Count];
        if (target == 0m) return result;
        var total = amounts.Sum();
        if (total <= 0m) throw new InvalidOperationException("A positive allocation requires a positive basis.");
        var fractions = new decimal[amounts.Count];
        for (var index = 0; index < amounts.Count; index++)
        {
            var cents = target * 100m * (amounts[index] / total);
            var floor = decimal.Floor(cents);
            result[index] = floor / 100m;
            fractions[index] = cents - floor;
        }
        var remaining = checked((int)((target - result.Sum()) * 100m));
        // Stable input order breaks equal-remainder ties; zero-basis rows never receive money.
        foreach (var index in Enumerable.Range(0, amounts.Count).Where(index => amounts[index] > 0m)
                     .OrderByDescending(index => fractions[index]).ThenBy(index => index).Take(remaining))
            result[index] += 0.01m;
        if (result.Sum() != target)
            throw new InvalidOperationException("The monetary allocation could not preserve its total.");
        return result;
    }
}
