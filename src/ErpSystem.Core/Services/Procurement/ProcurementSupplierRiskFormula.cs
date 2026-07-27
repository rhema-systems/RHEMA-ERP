namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementSupplierRiskFormula
{
    public static decimal CalculateWeightedScore(
        IEnumerable<ProcurementSupplierRiskWeightedScoreInput> dimensions)
    {
        var rows = dimensions.ToList();
        if (rows.Count == 0)
            throw new ArgumentException("At least one risk dimension is required.", nameof(dimensions));
        if (rows.Any(item => item.Score is < 0 or > 100 ||
                             item.WeightPercent <= 0 || item.WeightPercent > 100))
            throw new ArgumentOutOfRangeException(nameof(dimensions),
                "Scores and weights must be percentages within their valid ranges.");
        if (rows.Sum(item => item.WeightPercent) != 100)
            throw new ArgumentException("Risk dimension weights must total 100 percent.",
                nameof(dimensions));
        return decimal.Round(rows.Sum(item => item.Score * item.WeightPercent / 100), 2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateSpendShare(decimal supplierAmount, decimal tenantAmount)
    {
        if (supplierAmount < 0 || tenantAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(supplierAmount),
                "Spend amounts cannot be negative.");
        if (tenantAmount == 0) return 0;
        if (supplierAmount > tenantAmount)
            throw new ArgumentException("Supplier spend cannot exceed tenant spend.");
        return decimal.Round(supplierAmount / tenantAmount * 100, 2,
            MidpointRounding.AwayFromZero);
    }
}

public sealed record ProcurementSupplierRiskWeightedScoreInput(
    decimal Score,
    decimal WeightPercent);
