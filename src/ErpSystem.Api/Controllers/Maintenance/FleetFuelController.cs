using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/fuel")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetFuelController : ControllerBase
{
    private readonly IFleetFuelService _fleetFuelService;
    private readonly ILogger<FleetFuelController> _logger;

    public FleetFuelController(IFleetFuelService fleetFuelService, ILogger<FleetFuelController> logger)
    {
        _fleetFuelService = fleetFuelService;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/paged")]
    public async Task<ActionResult<PagedResult<FleetFuelTransactionDto>>> GetPaged(Guid vehicleAssetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try
        {
            var result = await _fleetFuelService.GetFuelTransactionsPagedAsync(vehicleAssetId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet fuel transactions for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving fuel transactions");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetFuelTransactionDto>> GetById(Guid id)
    {
        try
        {
            var item = await _fleetFuelService.GetByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet fuel transaction {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the fuel transaction");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetFuelTransactionDto>> Create([FromBody] CreateFleetFuelTransactionDto dto)
    {
        try
        {
            var created = await _fleetFuelService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet fuel transaction");
            return StatusCode(500, "An error occurred while creating the fuel transaction");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _fleetFuelService.DeleteAsync(id);
            return Ok(new { success = ok });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet fuel transaction {Id}", id);
            return StatusCode(500, "An error occurred while deleting the fuel transaction");
        }
    }
}

