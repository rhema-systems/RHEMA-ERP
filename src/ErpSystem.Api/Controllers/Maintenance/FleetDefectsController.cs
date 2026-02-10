using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/defects")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetDefectsController : ControllerBase
{
    private readonly IFleetDefectService _fleetDefectService;
    private readonly ILogger<FleetDefectsController> _logger;

    public FleetDefectsController(IFleetDefectService fleetDefectService, ILogger<FleetDefectsController> logger)
    {
        _fleetDefectService = fleetDefectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FleetDefectDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? vehicleAssetId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            var result = await _fleetDefectService.GetDefectsPagedAsync(page, pageSize, vehicleAssetId, status, searchTerm);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet defects");
            return StatusCode(500, "An error occurred while retrieving defects");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetDefectDto>> GetById(Guid id)
    {
        try
        {
            var item = await _fleetDefectService.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet defect {DefectId}", id);
            return StatusCode(500, "An error occurred while retrieving the defect");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetDefectDto>> Create([FromBody] CreateFleetDefectDto dto)
    {
        try
        {
            var created = await _fleetDefectService.CreateAsync(dto);
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
            _logger.LogError(ex, "Error creating fleet defect");
            return StatusCode(500, "An error occurred while creating the defect");
        }
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetDefectDto>> UpdateStatus(Guid id, [FromBody] UpdateFleetDefectStatusDto dto)
    {
        try
        {
            var updated = await _fleetDefectService.UpdateStatusAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet defect status {DefectId}", id);
            return StatusCode(500, "An error occurred while updating the defect");
        }
    }

    [HttpPost("work-orders")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> CreateWorkOrder([FromBody] CreateWorkOrderFromFleetDefectDto dto)
    {
        try
        {
            var workOrderId = await _fleetDefectService.CreateWorkOrderAsync(dto);
            return Ok(new { workOrderId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order from fleet defect {DefectId}", dto.DefectId);
            return StatusCode(500, "An error occurred while creating the work order");
        }
    }
}

