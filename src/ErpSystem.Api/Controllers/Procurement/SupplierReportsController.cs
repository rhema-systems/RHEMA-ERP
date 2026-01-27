using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-reports")]
[Authorize]
public class SupplierReportsController : ControllerBase
{
    private readonly ISupplierReportingService _reportingService;
    private readonly ILogger<SupplierReportsController> _logger;

    public SupplierReportsController(
        ISupplierReportingService reportingService,
        ILogger<SupplierReportsController> logger)
    {
        _reportingService = reportingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets supplier spend analysis report
    /// </summary>
    [HttpPost("spend-analysis")]
    public async Task<ActionResult<List<SupplierSpendAnalysisDto>>> GetSupplierSpendAnalysis([FromBody] SupplierSpendAnalysisRequest request)
    {
        try
        {
            var result = await _reportingService.GetSupplierSpendAnalysisAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier spend analysis");
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    /// <summary>
    /// Gets vendor concentration analysis
    /// </summary>
    [HttpGet("vendor-concentration")]
    public async Task<ActionResult<VendorConcentrationDto>> GetVendorConcentration(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _reportingService.GetVendorConcentrationAnalysisAsync(startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vendor concentration analysis");
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    /// <summary>
    /// Gets performance vs spend correlation analysis
    /// </summary>
    [HttpGet("performance-spend-correlation")]
    public async Task<ActionResult<List<PerformanceSpendCorrelationDto>>> GetPerformanceSpendCorrelation(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _reportingService.GetPerformanceSpendCorrelationAsync(startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance-spend correlation");
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    /// <summary>
    /// Gets supplier risk assessment report
    /// </summary>
    [HttpGet("risk-assessment")]
    public async Task<ActionResult<List<SupplierRiskAssessmentDto>>> GetSupplierRiskAssessment(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _reportingService.GetSupplierRiskAssessmentAsync(startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier risk assessment");
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    /// <summary>
    /// Exports supplier spend analysis to CSV
    /// </summary>
    [HttpPost("spend-analysis/export")]
    public async Task<IActionResult> ExportSupplierSpendAnalysis([FromBody] SupplierSpendAnalysisRequest request)
    {
        try
        {
            var data = await _reportingService.GetSupplierSpendAnalysisAsync(request);
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Supplier Code,Supplier Name,Total Spend,Total Orders,Average Order Value,Percentage of Total,First Order,Last Order,Performance Rating,Risk Level,Is Preferred,Is Blacklisted");
            
            foreach (var item in data)
            {
                csv.AppendLine($"{item.SupplierCode},{item.SupplierName},{item.TotalSpend:F2},{item.TotalOrders},{item.AverageOrderValue:F2},{item.PercentageOfTotalSpend:F2},{item.FirstOrderDate:yyyy-MM-dd},{item.LastOrderDate:yyyy-MM-dd},{item.PerformanceRating:F2},{item.RiskLevel},{item.IsPreferred},{item.IsBlacklisted}");
            }
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"supplier-spend-analysis-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting supplier spend analysis");
            return StatusCode(500, "An error occurred while exporting the report");
        }
    }

    /// <summary>
    /// Exports vendor concentration analysis to CSV
    /// </summary>
    [HttpGet("vendor-concentration/export")]
    public async Task<IActionResult> ExportVendorConcentration(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var data = await _reportingService.GetVendorConcentrationAnalysisAsync(startDate, endDate);
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Spend,{data.TotalSpend:F2}");
            csv.AppendLine($"Total Suppliers,{data.TotalSuppliers}");
            csv.AppendLine($"Top 10 Suppliers Spend,{data.Top10SuppliersSpend:F2}");
            csv.AppendLine($"Top 10 Percentage,{data.Top10Percentage:F2}%");
            csv.AppendLine($"Top 20 Suppliers Spend,{data.Top20SuppliersSpend:F2}");
            csv.AppendLine($"Top 20 Percentage,{data.Top20Percentage:F2}%");
            csv.AppendLine($"Concentration Risk,{data.ConcentrationRisk}");
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"vendor-concentration-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting vendor concentration");
            return StatusCode(500, "An error occurred while exporting the report");
        }
    }
}

