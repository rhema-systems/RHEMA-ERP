using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/trip-destinations")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetTripDestinationsController : ControllerBase
{
    private readonly IFleetTripDestinationService _service;
    private readonly ILogger<FleetTripDestinationsController> _logger;

    public FleetTripDestinationsController(IFleetTripDestinationService service, ILogger<FleetTripDestinationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FleetTripDestinationDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        try
        {
            var list = await _service.GetAllAsync(activeOnly);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet trip destinations");
            return StatusCode(500, "An error occurred while retrieving trip destinations");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetTripDestinationDto>> GetById(Guid id)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound($"Trip destination with ID {id} not found");
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet trip destination {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the trip destination");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetTripDestinationDto>> Create([FromBody] CreateFleetTripDestinationDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet trip destination");
            return StatusCode(500, "An error occurred while creating the trip destination");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetTripDestinationDto>> Update(Guid id, [FromBody] UpdateFleetTripDestinationDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet trip destination {Id}", id);
            return StatusCode(500, "An error occurred while updating the trip destination");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok) return NotFound($"Trip destination with ID {id} not found");
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet trip destination {Id}", id);
            return StatusCode(500, "An error occurred while deleting the trip destination");
        }
    }
}

