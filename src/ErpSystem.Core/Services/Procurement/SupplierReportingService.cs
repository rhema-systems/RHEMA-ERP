using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Service for generating supplier spend and performance reports
/// </summary>
public class SupplierReportingService : ISupplierReportingService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ILogger<SupplierReportingService> _logger;

    public SupplierReportingService(
        IPurchaseOrderRepository purchaseOrderRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ILogger<SupplierReportingService> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _logger = logger;
    }

    public async Task<List<SupplierSpendAnalysisDto>> GetSupplierSpendAnalysisAsync(SupplierSpendAnalysisRequest request)
    {
        _logger.LogInformation("Generating supplier spend analysis report");

        var startDate = request.StartDate ?? DateTime.UtcNow.AddYears(-1);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        // Get all purchase orders in the date range
        var orders = await _purchaseOrderRepository.GetOrdersByDateRangeAsync(startDate, endDate);

        // Filter by supplier IDs if provided
        if (request.SupplierIds != null && request.SupplierIds.Any())
        {
            orders = orders.Where(o => request.SupplierIds.Contains(o.SupplierId)).ToList();
        }

        // Group by supplier
        var supplierGroups = orders.GroupBy(o => o.SupplierId);

        var results = new List<SupplierSpendAnalysisDto>();
        decimal totalSpend = orders.Sum(o => o.TotalAmount);

        foreach (var group in supplierGroups)
        {
            var supplier = await _businessPartnerRepository.GetByIdAsync(group.Key);
            if (supplier == null) continue;

            // Skip blacklisted suppliers if requested
            if (!request.IncludeBlacklisted && supplier.IsBlacklisted) continue;

            var supplierOrders = group.ToList();
            var supplierSpend = supplierOrders.Sum(o => o.TotalAmount);

            results.Add(new SupplierSpendAnalysisDto
            {
                SupplierId = supplier.Id,
                SupplierCode = supplier.PartnerCode,
                SupplierName = supplier.PartnerName,
                TotalSpend = supplierSpend,
                TotalOrders = supplierOrders.Count,
                AverageOrderValue = supplierOrders.Count > 0 ? supplierSpend / supplierOrders.Count : 0,
                PercentageOfTotalSpend = totalSpend > 0 ? (supplierSpend / totalSpend) * 100 : 0,
                FirstOrderDate = supplierOrders.Min(o => o.OrderDate),
                LastOrderDate = supplierOrders.Max(o => o.OrderDate),
                PerformanceRating = supplier.PerformanceRating,
                RiskLevel = supplier.RiskLevel,
                IsPreferred = supplier.IsPreferred,
                IsBlacklisted = supplier.IsBlacklisted
            });
        }

        // Sort results
        results = request.SortBy switch
        {
            "OrderCount" => results.OrderByDescending(r => r.TotalOrders).ToList(),
            "AverageOrderValue" => results.OrderByDescending(r => r.AverageOrderValue).ToList(),
            _ => results.OrderByDescending(r => r.TotalSpend).ToList()
        };

        // Take top N if specified
        if (request.TopN.HasValue && request.TopN.Value > 0)
        {
            results = results.Take(request.TopN.Value).ToList();
        }

        _logger.LogInformation("Generated spend analysis for {Count} suppliers", results.Count);
        return results;
    }

    public async Task<VendorConcentrationDto> GetVendorConcentrationAnalysisAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Generating vendor concentration analysis");

        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow;

        var spendAnalysis = await GetSupplierSpendAnalysisAsync(new SupplierSpendAnalysisRequest
        {
            StartDate = start,
            EndDate = end,
            IncludeBlacklisted = false
        });

        var totalSpend = spendAnalysis.Sum(s => s.TotalSpend);
        var totalSuppliers = spendAnalysis.Count;

        var top10 = spendAnalysis.Take(10).ToList();
        var top10Spend = top10.Sum(s => s.TotalSpend);
        var top10Percentage = totalSpend > 0 ? (top10Spend / totalSpend) * 100 : 0;

        var top20 = spendAnalysis.Take(20).ToList();
        var top20Spend = top20.Sum(s => s.TotalSpend);
        var top20Percentage = totalSpend > 0 ? (top20Spend / totalSpend) * 100 : 0;

        // Determine concentration risk
        var concentrationRisk = top10Percentage switch
        {
            >= 80 => "High",
            >= 60 => "Medium",
            _ => "Low"
        };

        return new VendorConcentrationDto
        {
            TotalSpend = totalSpend,
            TotalSuppliers = totalSuppliers,
            Top10SuppliersCount = top10.Count,
            Top10SuppliersSpend = top10Spend,
            Top10Percentage = top10Percentage,
            Top20SuppliersCount = top20.Count,
            Top20SuppliersSpend = top20Spend,
            Top20Percentage = top20Percentage,
            TopSuppliers = top10,
            ConcentrationRisk = concentrationRisk
        };
    }

    public async Task<List<PerformanceSpendCorrelationDto>> GetPerformanceSpendCorrelationAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Generating performance vs spend correlation analysis");

        var spendAnalysis = await GetSupplierSpendAnalysisAsync(new SupplierSpendAnalysisRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludeBlacklisted = false
        });

        var totalSpend = spendAnalysis.Sum(s => s.TotalSpend);

        // Group by performance grade
        var performanceGroups = new List<PerformanceSpendCorrelationDto>
        {
            new() { PerformanceGrade = "A (4.5-5.0)", PerformanceRatingMin = 4.5m, PerformanceRatingMax = 5.0m },
            new() { PerformanceGrade = "B (3.5-4.4)", PerformanceRatingMin = 3.5m, PerformanceRatingMax = 4.4m },
            new() { PerformanceGrade = "C (2.5-3.4)", PerformanceRatingMin = 2.5m, PerformanceRatingMax = 3.4m },
            new() { PerformanceGrade = "D (1.5-2.4)", PerformanceRatingMin = 1.5m, PerformanceRatingMax = 2.4m },
            new() { PerformanceGrade = "F (0.0-1.4)", PerformanceRatingMin = 0.0m, PerformanceRatingMax = 1.4m },
            new() { PerformanceGrade = "Not Rated", PerformanceRatingMin = -1m, PerformanceRatingMax = -1m }
        };

        foreach (var group in performanceGroups)
        {
            var suppliers = group.PerformanceGrade == "Not Rated"
                ? spendAnalysis.Where(s => !s.PerformanceRating.HasValue).ToList()
                : spendAnalysis.Where(s => s.PerformanceRating.HasValue &&
                                          s.PerformanceRating.Value >= group.PerformanceRatingMin &&
                                          s.PerformanceRating.Value <= group.PerformanceRatingMax).ToList();

            var groupSpend = suppliers.Sum(s => s.TotalSpend);

            group.SupplierCount = suppliers.Count;
            group.TotalSpend = groupSpend;
            group.AverageSpendPerSupplier = suppliers.Count > 0 ? groupSpend / suppliers.Count : 0;
            group.PercentageOfTotalSpend = totalSpend > 0 ? (groupSpend / totalSpend) * 100 : 0;
        }

        return performanceGroups.Where(g => g.SupplierCount > 0).ToList();
    }

    public async Task<List<SupplierRiskAssessmentDto>> GetSupplierRiskAssessmentAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Generating supplier risk assessment report");

        var spendAnalysis = await GetSupplierSpendAnalysisAsync(new SupplierSpendAnalysisRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludeBlacklisted = true // Include all suppliers for risk assessment
        });

        var totalSpend = spendAnalysis.Sum(s => s.TotalSpend);
        var results = new List<SupplierRiskAssessmentDto>();

        foreach (var supplier in spendAnalysis)
        {
            var partner = await _businessPartnerRepository.GetByIdAsync(supplier.SupplierId);
            if (partner == null) continue;

            var riskFactors = new List<string>();

            // Identify risk factors
            if (partner.IsBlacklisted)
            {
                riskFactors.Add("Currently blacklisted");
            }

            if (partner.RiskLevel == "High" || partner.RiskLevel == "Critical")
            {
                riskFactors.Add($"{partner.RiskLevel} risk level");
            }

            if (partner.PerformanceRating.HasValue && partner.PerformanceRating.Value < 2.5m)
            {
                riskFactors.Add($"Low performance rating ({partner.PerformanceRating.Value:F2})");
            }

            var expiredLicenses = partner.Licenses.Count(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value < DateTime.UtcNow);
            if (expiredLicenses > 0)
            {
                riskFactors.Add($"{expiredLicenses} expired licenses");
            }

            if (!string.IsNullOrEmpty(partner.CreditRating))
            {
                var lowRatings = new[] { "D", "C-", "C" };
                if (lowRatings.Contains(partner.CreditRating))
                {
                    riskFactors.Add($"Low credit rating ({partner.CreditRating})");
                }
            }

            // Only include suppliers with risk factors or high spend
            if (riskFactors.Any() || supplier.PercentageOfTotalSpend > 5)
            {
                results.Add(new SupplierRiskAssessmentDto
                {
                    SupplierId = supplier.SupplierId,
                    SupplierName = supplier.SupplierName,
                    RiskLevel = partner.RiskLevel ?? "Unknown",
                    TotalSpend = supplier.TotalSpend,
                    PercentageOfTotalSpend = supplier.PercentageOfTotalSpend,
                    RiskFactors = riskFactors,
                    PerformanceRating = partner.PerformanceRating,
                    IsBlacklisted = partner.IsBlacklisted,
                    ExpiredLicensesCount = expiredLicenses,
                    CreditRating = partner.CreditRating
                });
            }
        }

        return results.OrderByDescending(r => r.PercentageOfTotalSpend).ToList();
    }
}

/// <summary>
/// Service interface for supplier reporting
/// </summary>
public interface ISupplierReportingService
{
    Task<List<SupplierSpendAnalysisDto>> GetSupplierSpendAnalysisAsync(SupplierSpendAnalysisRequest request);
    Task<VendorConcentrationDto> GetVendorConcentrationAnalysisAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<PerformanceSpendCorrelationDto>> GetPerformanceSpendCorrelationAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<SupplierRiskAssessmentDto>> GetSupplierRiskAssessmentAsync(DateTime? startDate = null, DateTime? endDate = null);
}

