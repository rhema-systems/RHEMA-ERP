namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyCostDashboardLineDto
{
    public Guid BoqItemId { get; init; }
    public Guid ProjectPackageId { get; init; }
    public string? PackageCode { get; init; }
    public string? PackageName { get; init; }
    public string? SectionCode { get; init; }
    public string? SectionName { get; init; }
    public string? CostCode { get; init; }
    public string? CostCodeName { get; init; }
    public string? LineNumber { get; init; }
    public string? ItemCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? UnitOfMeasure { get; init; }
    public decimal BudgetAmount { get; init; }
    public decimal CommittedAmount { get; init; }
    public decimal ActualAmount { get; init; }
    public decimal ForecastAmount { get; init; }
    public decimal ForecastVarianceAmount { get; init; }
}

public sealed class QuantitySurveyCostDashboardDto
{
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectTitle { get; init; } = string.Empty;
    public string ProjectStatus { get; init; } = string.Empty;
    public Guid? ContractId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public decimal ApprovedBudget { get; init; }
    public decimal CommittedValue { get; init; }
    public decimal CertifiedValue { get; init; }
    public decimal ActualCost { get; init; }
    public decimal ApprovedVariationValue { get; init; }
    public decimal ForecastCost { get; init; }
    public decimal FinalProjectedCost { get; init; }
    public decimal CostToComplete { get; init; }
    public decimal BudgetVariance { get; init; }
    public string ForecastBasis { get; init; } = string.Empty;
    public bool HasConversionGaps { get; init; }
    public int MissingExchangeRateCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<QuantitySurveyCostDashboardLineDto> Lines { get; init; } = [];
}
