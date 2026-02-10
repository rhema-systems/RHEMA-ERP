using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/batteries")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetBatteriesController : ControllerBase
{
    private readonly IFleetBatteryService _fleetBatteryService;
    private readonly ILogger<FleetBatteriesController> _logger;

    public FleetBatteriesController(IFleetBatteryService fleetBatteryService, ILogger<FleetBatteriesController> logger)
    {
        _fleetBatteryService = fleetBatteryService;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}")]
    public async Task<ActionResult<PagedResult<FleetBatteryDto>>> GetForVehicle(Guid vehicleAssetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try
        {
            var result = await _fleetBatteryService.GetPagedAsync(vehicleAssetId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet batteries for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving batteries");
        }
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/kpis")]
    public async Task<ActionResult<FleetBatteryKpisDto>> GetKpis(Guid vehicleAssetId)
    {
        try
        {
            var kpis = await _fleetBatteryService.GetKpisAsync(vehicleAssetId);
            return Ok(kpis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet battery KPIs for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving battery KPIs");
        }
    }

    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<IEnumerable<FleetBatteryEventDto>>> GetEvents(Guid id)
    {
        try
        {
            var items = await _fleetBatteryService.GetEventsAsync(id);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet battery events {Id}", id);
            return StatusCode(500, "An error occurred while retrieving battery history");
        }
    }

    [HttpPost]
    public async Task<ActionResult<FleetBatteryDto>> Create([FromBody] CreateFleetBatteryDto dto)
    {
        try
        {
            var created = await _fleetBatteryService.CreateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet battery");
            return StatusCode(500, "An error occurred while creating the battery");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FleetBatteryDto>> Update(Guid id, [FromBody] CreateFleetBatteryDto dto)
    {
        try
        {
            var updated = await _fleetBatteryService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet battery {Id}", id);
            return StatusCode(500, "An error occurred while updating the battery");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _fleetBatteryService.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet battery {Id}", id);
            return StatusCode(500, "An error occurred while deleting the battery");
        }
    }
}

