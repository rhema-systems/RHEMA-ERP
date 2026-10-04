namespace ErpSystem.Api.Services.Finance;

using ErpSystem.Core.Finance;

internal static class InvoiceTradeDiscountPolicy
{
    internal static decimal CalculateLineDiscount(
        decimal grossAmount,
        decimal discountPercentage,
        string documentLabel,
        int currencyDecimalPlaces = 2)
    {
        ValidatePercentage(discountPercentage, documentLabel);
        return RoundMoney(RoundMoney(grossAmount, currencyDecimalPlaces) * discountPercentage / 100m, currencyDecimalPlaces);
    }

    internal static void ValidatePercentage(decimal discountPercentage, string documentLabel)
    {
        if (discountPercentage is < 0m or > 100m)
        {
            throw new InvalidOperationException(
                $"{documentLabel} trade discount percentage must be between 0 and 100.");
        }
    }

    internal static IReadOnlyDictionary<Guid, decimal> AllocateDocumentDiscount(
        decimal documentDiscount,
        IEnumerable<(Guid SourceLineId, decimal NetLineAmount)> sourceLines,
        string documentLabel,
        int currencyDecimalPlaces = 2)
    {
        var roundedDiscount = RoundMoney(documentDiscount, currencyDecimalPlaces);
        if (roundedDiscount < 0m)
            throw new InvalidOperationException($"{documentLabel} document trade discount cannot be negative.");
        if (roundedDiscount == 0m)
            return new Dictionary<Guid, decimal>();

        var eligible = sourceLines
            .Select(line => new
            {
                line.SourceLineId,
                Basis = RoundMoney(Math.Max(0m, line.NetLineAmount), currencyDecimalPlaces)
            })
            .Where(line => line.Basis > 0m)
            .OrderBy(line => line.SourceLineId.ToString("D"), StringComparer.Ordinal)
            .ToList();
        var totalBasis = eligible.Sum(line => line.Basis);
        if (eligible.Count == 0 || roundedDiscount > totalBasis)
        {
            throw new InvalidOperationException(
                $"{documentLabel} document trade discount exceeds the eligible source-line amount.");
        }

        var allocations = new Dictionary<Guid, decimal>();
        var allocated = 0m;
        for (var index = 0; index < eligible.Count; index++)
        {
            var allocation = index == eligible.Count - 1
                ? roundedDiscount - allocated
                : RoundMoney(roundedDiscount * eligible[index].Basis / totalBasis, currencyDecimalPlaces);
            allocations[eligible[index].SourceLineId] = allocation;
            allocated += allocation;
        }

        return allocations;
    }

    private static decimal RoundMoney(decimal amount, int currencyDecimalPlaces) =>
        CurrencyMinorUnitPolicy.Round(amount, currencyDecimalPlaces);
}
