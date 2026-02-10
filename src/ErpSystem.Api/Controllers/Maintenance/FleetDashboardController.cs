using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/dashboard")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetDashboardController : ControllerBase
{
    private readonly IFleetDashboardService _dashboard;
    private readonly ILogger<FleetDashboardController> _logger;

    public FleetDashboardController(IFleetDashboardService dashboard, ILogger<FleetDashboardController> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<FleetDashboardSummaryDto>> GetSummary([FromQuery] Guid? vehicleAssetId = null)
    {
        try
        {
            var result = await _dashboard.GetSummaryAsync(vehicleAssetId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet dashboard summary");
            return StatusCode(500, "An error occurred while retrieving fleet dashboard summary");
        }
    }
}

