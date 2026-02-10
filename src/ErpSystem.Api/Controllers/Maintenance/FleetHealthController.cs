using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/health")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetHealthController : ControllerBase
{
    private readonly IFleetHealthService _health;
    private readonly ILogger<FleetHealthController> _logger;

    public FleetHealthController(IFleetHealthService health, ILogger<FleetHealthController> logger)
    {
        _health = health;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<FleetHealthDto>> Get()
    {
        try
        {
            return Ok(await _health.GetHealthAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet health");
            return StatusCode(500, "An error occurred while retrieving fleet health");
        }
    }
}
