using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/costs")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetCostsController : ControllerBase
{
    private readonly IFleetCostService _service;
    private readonly ILogger<FleetCostsController> _logger;

    public FleetCostsController(IFleetCostService service, ILogger<FleetCostsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}")]
    public async Task<ActionResult<PagedResult<FleetCostEntryDto>>> GetForVehicle(
        Guid vehicleAssetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        try
        {
            var result = await _service.GetPagedAsync(vehicleAssetId, page, pageSize, fromUtc, toUtc);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet costs for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving costs");
        }
    }

    [HttpGet("trip/{fleetTripId:guid}")]
    public async Task<ActionResult<PagedResult<FleetCostEntryDto>>> GetForTrip(
        Guid fleetTripId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        try
        {
            var result = await _service.GetPagedForTripAsync(fleetTripId, page, pageSize, fromUtc, toUtc);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet costs for trip {FleetTripId}", fleetTripId);
            return StatusCode(500, "An error occurred while retrieving costs");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetCostEntryDto>> Create([FromBody] CreateFleetCostEntryDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet cost entry");
            return StatusCode(500, "An error occurred while creating the cost entry");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet cost entry {Id}", id);
            return StatusCode(500, "An error occurred while deleting the cost entry");
        }
    }
}

