using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-performance")]
[Authorize]
public class SupplierPerformanceController : ControllerBase
{
    private readonly ISupplierPerformanceService _performanceService;
    private readonly ILogger<SupplierPerformanceController> _logger;

    public SupplierPerformanceController(
        ISupplierPerformanceService performanceService,
        ILogger<SupplierPerformanceController> logger)
    {
        _performanceService = performanceService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierPerformanceMetricDto>> GetById(Guid id)
    {
        try
        {
            var metric = await _performanceService.GetByIdAsync(id);
            if (metric == null)
            {
                return NotFound($"Performance metric with ID {id} not found");
            }
            return Ok(metric);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metric {MetricId}", id);
            return StatusCode(500, "An error occurred while retrieving the performance metric");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<SupplierPerformanceMetricDto>>> GetByBusinessPartner(Guid businessPartnerId)
    {
        try
        {
            var metrics = await _performanceService.GetByBusinessPartnerAsync(businessPartnerId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving performance metrics");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}/trends")]
    public async Task<ActionResult<IEnumerable<PerformanceTrendDto>>> GetTrends(Guid businessPartnerId, [FromQuery] int numberOfPeriods = 12)
    {
        try
        {
            var trends = await _performanceService.GetTrendsAsync(businessPartnerId, numberOfPeriods);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance trends for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving performance trends");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}/report-card")]
    public async Task<ActionResult<PerformanceReportCardDto>> GetReportCard(Guid businessPartnerId, [FromQuery] string reportPeriod = "Current")
    {
        try
        {
            var reportCard = await _performanceService.GetReportCardAsync(businessPartnerId, reportPeriod);
            return Ok(reportCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report card for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while generating the report card");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SupplierPerformanceMetricDto>> Create([FromBody] CreateSupplierPerformanceMetricDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var metric = await _performanceService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = metric.Id }, metric);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance metric");
            return StatusCode(500, "An error occurred while creating the performance metric");
        }
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<SupplierPerformanceMetricDto>> CalculateMetrics([FromBody] CreateSupplierPerformanceMetricDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var metric = await _performanceService.CalculateMetricsAsync(
                createDto.BusinessPartnerId,
                createDto.MetricPeriod,
                createDto.Year,
                createDto.Month,
                createDto.Quarter);

            return Ok(metric);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating performance metrics");
            return StatusCode(500, "An error occurred while calculating performance metrics");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _performanceService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance metric {MetricId}", id);
            return StatusCode(500, "An error occurred while deleting the performance metric");
        }
    }
}

