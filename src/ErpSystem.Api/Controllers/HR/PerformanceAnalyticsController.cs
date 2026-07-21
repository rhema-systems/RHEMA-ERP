using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceAnalyticsController : ControllerBase
{
    private readonly IPerformanceAnalyticsService _service;
    private readonly ILogger<PerformanceAnalyticsController> _logger;

    public PerformanceAnalyticsController(IPerformanceAnalyticsService service, ILogger<PerformanceAnalyticsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Rating distribution for a cycle (calibration leniency/skew)</summary>
    [HttpGet("cycle/{cycleId:guid}/rating-distribution")]
    [Authorize(Roles = "SuperAdmin,HR")]
    [ProducesResponseType(typeof(CalibrationDistributionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCycleRatingDistribution(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetCycleRatingDistributionAsync(cycleId, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building rating distribution for cycle {Id}", cycleId);
            return StatusCode(500, "An error occurred while building the rating distribution");
        }
    }

    /// <summary>An employee's multi-year appraisal score trend</summary>
    [HttpGet("employee/{employeeId:guid}/trend")]
    [ProducesResponseType(typeof(EmployeePerformanceTrendDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeTrend(Guid employeeId, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetEmployeeTrendAsync(employeeId, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building trend for employee {Id}", employeeId);
            return StatusCode(500, "An error occurred while building the performance trend");
        }
    }
}
