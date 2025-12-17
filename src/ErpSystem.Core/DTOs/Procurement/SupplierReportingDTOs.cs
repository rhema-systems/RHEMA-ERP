namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Supplier spend analysis report
/// </summary>
public class SupplierSpendAnalysisDto
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalSpend { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal PercentageOfTotalSpend { get; set; }
    public DateTime? FirstOrderDate { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public decimal? PerformanceRating { get; set; }
    public string? RiskLevel { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsBlacklisted { get; set; }
}

/// <summary>
/// Vendor concentration analysis
/// </summary>
public class VendorConcentrationDto
{
    public decimal TotalSpend { get; set; }
    public int TotalSuppliers { get; set; }
    public int Top10SuppliersCount { get; set; }
    public decimal Top10SuppliersSpend { get; set; }
    public decimal Top10Percentage { get; set; }
    public int Top20SuppliersCount { get; set; }
    public decimal Top20SuppliersSpend { get; set; }
    public decimal Top20Percentage { get; set; }
    public List<SupplierSpendAnalysisDto> TopSuppliers { get; set; } = new();
    public string ConcentrationRisk { get; set; } = "Low"; // Low, Medium, High
}

/// <summary>
/// Category spend analysis
/// </summary>
public class CategorySpendAnalysisDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalSpend { get; set; }
    public int TotalOrders { get; set; }
    public int SupplierCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal PercentageOfTotalSpend { get; set; }
    public List<SupplierSpendAnalysisDto> TopSuppliers { get; set; } = new();
}

/// <summary>
/// Payment terms analysis
/// </summary>
public class PaymentTermsAnalysisDto
{
    public string PaymentTerms { get; set; } = string.Empty;
    public int SupplierCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal PercentageOfTotalSpend { get; set; }
    public decimal AveragePaymentDays { get; set; }
}

/// <summary>
/// Supplier performance vs spend correlation
/// </summary>
public class PerformanceSpendCorrelationDto
{
    public string PerformanceGrade { get; set; } = string.Empty;
    public decimal PerformanceRatingMin { get; set; }
    public decimal PerformanceRatingMax { get; set; }
    public int SupplierCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal AverageSpendPerSupplier { get; set; }
    public decimal PercentageOfTotalSpend { get; set; }
}

/// <summary>
/// Supplier spend trend over time
/// </summary>
public class SupplierSpendTrendDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? Quarter { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalSpend { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}

/// <summary>
/// Request for supplier spend analysis
/// </summary>
public class SupplierSpendAnalysisRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<Guid>? SupplierIds { get; set; }
    public List<Guid>? CategoryIds { get; set; }
    public int? TopN { get; set; } = 10;
    public string? SortBy { get; set; } = "TotalSpend"; // TotalSpend, OrderCount, AverageOrderValue
    public bool IncludeBlacklisted { get; set; } = false;
}

/// <summary>
/// Comprehensive supplier report
/// </summary>
public class ComprehensiveSupplierReportDto
{
    public DateTime ReportDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalSpend { get; set; }
    public int TotalOrders { get; set; }
    public int ActiveSuppliers { get; set; }
    public List<SupplierSpendAnalysisDto> TopSuppliers { get; set; } = new();
    public VendorConcentrationDto VendorConcentration { get; set; } = null!;
    public List<CategorySpendAnalysisDto> CategorySpend { get; set; } = new();
    public List<PaymentTermsAnalysisDto> PaymentTermsAnalysis { get; set; } = new();
    public List<PerformanceSpendCorrelationDto> PerformanceCorrelation { get; set; } = new();
}

/// <summary>
/// Supplier risk assessment report
/// </summary>
public class SupplierRiskAssessmentDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public decimal TotalSpend { get; set; }
    public decimal PercentageOfTotalSpend { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public decimal? PerformanceRating { get; set; }
    public bool IsBlacklisted { get; set; }
    public int ExpiredLicensesCount { get; set; }
    public string? CreditRating { get; set; }
}

