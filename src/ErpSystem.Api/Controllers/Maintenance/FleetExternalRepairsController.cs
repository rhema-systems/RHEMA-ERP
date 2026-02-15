using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/external-repairs")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetExternalRepairsController : ControllerBase
{
    private readonly IFleetExternalRepairService _service;
    private readonly ILogger<FleetExternalRepairsController> _logger;

    public FleetExternalRepairsController(IFleetExternalRepairService service, ILogger<FleetExternalRepairsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FleetExternalRepairDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? vehicleAssetId = null,
        [FromQuery] string? status = null)
    {
        try
        {
            var result = await _service.GetPagedAsync(page, pageSize, vehicleAssetId, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving external repairs");
            return StatusCode(500, "An error occurred while retrieving external repairs");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetExternalRepairDto>> GetById(Guid id)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving external repair {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetExternalRepairDto>> Create([FromBody] CreateFleetExternalRepairDto dto)
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
            _logger.LogError(ex, "Error creating external repair");
            return StatusCode(500, "An error occurred while creating the record");
        }
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetExternalRepairDto>> UpdateStatus(Guid id, [FromBody] UpdateFleetExternalRepairStatusDto dto)
    {
        try
        {
            var updated = await _service.UpdateStatusAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating external repair status {Id}", id);
            return StatusCode(500, "An error occurred while updating the record");
        }
    }
}

