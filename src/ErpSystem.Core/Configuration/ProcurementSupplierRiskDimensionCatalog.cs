namespace ErpSystem.Core.Configuration;

public enum ProcurementSupplierRiskDimension
{
    SupplierPerformance,
    Delivery,
    Quality,
    CostCompetitiveness,
    Compliance,
    DueDiligence,
    FinancialStability,
    SpendDiversification,
    SingleSourceDependency
}

/// <summary>
/// The shared contract between DEC-011 configuration and the risk engine.
/// Names are explicit: unsupported labels must not silently become aliases.
/// </summary>
public static class ProcurementSupplierRiskDimensionCatalog
{
    private static readonly IReadOnlyDictionary<string, ProcurementSupplierRiskDimension> Dimensions =
        new Dictionary<string, ProcurementSupplierRiskDimension>(StringComparer.OrdinalIgnoreCase)
        {
            ["SupplierPerformance"] = ProcurementSupplierRiskDimension.SupplierPerformance,
            ["Performance"] = ProcurementSupplierRiskDimension.SupplierPerformance,
            ["OverallPerformance"] = ProcurementSupplierRiskDimension.SupplierPerformance,
            ["Delivery"] = ProcurementSupplierRiskDimension.Delivery,
            ["Quality"] = ProcurementSupplierRiskDimension.Quality,
            ["CostCompetitiveness"] = ProcurementSupplierRiskDimension.CostCompetitiveness,
            ["Compliance"] = ProcurementSupplierRiskDimension.Compliance,
            ["DueDiligence"] = ProcurementSupplierRiskDimension.DueDiligence,
            ["FinancialStability"] = ProcurementSupplierRiskDimension.FinancialStability,
            ["SpendDiversification"] = ProcurementSupplierRiskDimension.SpendDiversification,
            ["SingleSourceDependency"] = ProcurementSupplierRiskDimension.SingleSourceDependency
        };

    public static IReadOnlyList<string> SupportedNames { get; } =
        Array.AsReadOnly(Dimensions.Keys.ToArray());

    public static bool TryResolve(string? name, out ProcurementSupplierRiskDimension dimension) =>
        Dimensions.TryGetValue(name?.Trim() ?? string.Empty, out dimension);
}
