using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/reports")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetReportsController : ControllerBase
{
    private readonly IFleetReportsService _reports;
    private readonly ILogger<FleetReportsController> _logger;

    public FleetReportsController(IFleetReportsService reports, ILogger<FleetReportsController> logger)
    {
        _reports = reports;
        _logger = logger;
    }

    [HttpGet("cost-summary")]
    public async Task<ActionResult<FleetCostSummaryDto>> GetCostSummary(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int top = 10,
        [FromQuery] Guid? vehicleAssetId = null)
    {
        try
        {
            return Ok(await _reports.GetCostSummaryAsync(fromUtc, toUtc, top, vehicleAssetId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet cost summary report");
            return StatusCode(500, "An error occurred while retrieving fleet cost summary report");
        }
    }

    [HttpGet("utilization")]
    public async Task<ActionResult<FleetUtilizationSummaryDto>> GetUtilization(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int top = 10,
        [FromQuery] Guid? vehicleAssetId = null)
    {
        try
        {
            return Ok(await _reports.GetUtilizationSummaryAsync(fromUtc, toUtc, top, vehicleAssetId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet utilization report");
            return StatusCode(500, "An error occurred while retrieving fleet utilization report");
        }
    }
}
