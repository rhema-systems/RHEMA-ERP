namespace ErpSystem.Api.Services.Finance;

internal static class InvoiceTradeDiscountPolicy
{
    internal static decimal CalculateLineDiscount(
        decimal grossAmount,
        decimal discountPercentage,
        string documentLabel)
    {
        ValidatePercentage(discountPercentage, documentLabel);
        return RoundMoney(RoundMoney(grossAmount) * discountPercentage / 100m);
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
        string documentLabel)
    {
        var roundedDiscount = RoundMoney(documentDiscount);
        if (roundedDiscount < 0m)
            throw new InvalidOperationException($"{documentLabel} document trade discount cannot be negative.");
        if (roundedDiscount == 0m)
            return new Dictionary<Guid, decimal>();

        var eligible = sourceLines
            .Select(line => new
            {
                line.SourceLineId,
                Basis = RoundMoney(Math.Max(0m, line.NetLineAmount))
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
                : RoundMoney(roundedDiscount * eligible[index].Basis / totalBasis);
            allocations[eligible[index].SourceLineId] = allocation;
            allocated += allocation;
        }

        return allocations;
    }

    private static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
