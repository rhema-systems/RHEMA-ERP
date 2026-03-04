using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/compliance")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetComplianceController : ControllerBase
{
    private readonly IFleetComplianceService _fleetComplianceService;
    private readonly IFleetComplianceTemplateService _templateService;
    private readonly ILogger<FleetComplianceController> _logger;

    public FleetComplianceController(
        IFleetComplianceService fleetComplianceService,
        IFleetComplianceTemplateService templateService,
        ILogger<FleetComplianceController> logger)
    {
        _fleetComplianceService = fleetComplianceService;
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/paged")]
    public async Task<ActionResult<PagedResult<FleetComplianceItemDto>>> GetPaged(Guid vehicleAssetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try
        {
            var result = await _fleetComplianceService.GetComplianceItemsPagedAsync(vehicleAssetId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet compliance items for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving compliance items");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetComplianceItemDto>> GetById(Guid id)
    {
        try
        {
            var item = await _fleetComplianceService.GetByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet compliance item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the compliance item");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetComplianceItemDto>> Create([FromBody] CreateFleetComplianceItemDto dto)
    {
        try
        {
            var created = await _fleetComplianceService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet compliance item");
            return StatusCode(500, "An error occurred while creating the compliance item");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetComplianceItemDto>> Update(Guid id, [FromBody] UpdateFleetComplianceItemDto dto)
    {
        try
        {
            var updated = await _fleetComplianceService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet compliance item {Id}", id);
            return StatusCode(500, "An error occurred while updating the compliance item");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _fleetComplianceService.DeleteAsync(id);
            return Ok(new { success = ok });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet compliance item {Id}", id);
            return StatusCode(500, "An error occurred while deleting the compliance item");
        }
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/dispatch-blocking")]
    public async Task<ActionResult<List<FleetComplianceItemDto>>> GetDispatchBlocking(Guid vehicleAssetId)
    {
        try
        {
            var items = await _fleetComplianceService.GetDispatchBlockingItemsAsync(vehicleAssetId);
            return Ok(items);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dispatch-blocking compliance items for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving dispatch-blocking compliance items");
        }
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/template")]
    public async Task<ActionResult<FleetVehicleComplianceTemplateDto?>> GetVehicleTemplate(Guid vehicleAssetId)
    {
        try
        {
            var result = await _templateService.GetVehicleTemplateAsync(vehicleAssetId);
            return Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance template for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving the vehicle template");
        }
    }

    [HttpPost("vehicle/{vehicleAssetId:guid}/template/{templateId:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetVehicleComplianceTemplateDto>> ApplyTemplate(Guid vehicleAssetId, Guid templateId)
    {
        try
        {
            var result = await _templateService.ApplyTemplateToVehicleAsync(vehicleAssetId, templateId);
            return Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying compliance template {TemplateId} to vehicle {VehicleAssetId}", templateId, vehicleAssetId);
            return StatusCode(500, "An error occurred while applying the template");
        }
    }
}

