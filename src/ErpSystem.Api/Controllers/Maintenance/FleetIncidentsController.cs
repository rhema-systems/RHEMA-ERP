using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/incidents")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetIncidentsController : ControllerBase
{
    private readonly IFleetIncidentService _service;
    private readonly ILogger<FleetIncidentsController> _logger;

    public FleetIncidentsController(IFleetIncidentService service, ILogger<FleetIncidentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FleetIncidentDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? vehicleAssetId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            var result = await _service.GetPagedAsync(page, pageSize, vehicleAssetId, status, searchTerm);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet incidents");
            return StatusCode(500, "An error occurred while retrieving incidents");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetIncidentDto>> GetById(Guid id)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while retrieving the incident");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetIncidentDto>> Create([FromBody] CreateFleetIncidentDto dto)
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet incident");
            return StatusCode(500, "An error occurred while creating the incident");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetIncidentDto>> Update(Guid id, [FromBody] UpdateFleetIncidentDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while updating the incident");
        }
    }

    [HttpPost("work-orders")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> CreateWorkOrder([FromBody] CreateWorkOrderFromFleetIncidentDto dto)
    {
        try
        {
            var workOrderId = await _service.CreateWorkOrderAsync(dto);
            return Ok(new { workOrderId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database update error creating work order from fleet incident {IncidentId}", dto.IncidentId);
            return BadRequest("Unable to create the work order due to a database constraint. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order from fleet incident {IncidentId}", dto.IncidentId);
            return StatusCode(500, "An error occurred while creating the work order");
        }
    }
}

