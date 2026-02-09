using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/tyres")]
[Authorize]
public class FleetTyresController : ControllerBase
{
    private readonly IFleetTyreService _fleetTyreService;
    private readonly ILogger<FleetTyresController> _logger;

    public FleetTyresController(IFleetTyreService fleetTyreService, ILogger<FleetTyresController> logger)
    {
        _fleetTyreService = fleetTyreService;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}")]
    public async Task<ActionResult<PagedResult<FleetTyreDto>>> GetForVehicle(Guid vehicleAssetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try
        {
            var result = await _fleetTyreService.GetPagedAsync(vehicleAssetId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet tyres for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving tyres");
        }
    }

    [HttpPost]
    public async Task<ActionResult<FleetTyreDto>> Create([FromBody] CreateFleetTyreDto dto)
    {
        try
        {
            var created = await _fleetTyreService.CreateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet tyre");
            return StatusCode(500, "An error occurred while creating the tyre");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FleetTyreDto>> Update(Guid id, [FromBody] CreateFleetTyreDto dto)
    {
        try
        {
            var updated = await _fleetTyreService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet tyre {Id}", id);
            return StatusCode(500, "An error occurred while updating the tyre");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _fleetTyreService.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet tyre {Id}", id);
            return StatusCode(500, "An error occurred while deleting the tyre");
        }
    }
}

