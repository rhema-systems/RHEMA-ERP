using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/compliance/templates")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetComplianceTemplatesController : ControllerBase
{
    private readonly IFleetComplianceTemplateService _templateService;
    private readonly ILogger<FleetComplianceTemplatesController> _logger;

    public FleetComplianceTemplatesController(IFleetComplianceTemplateService templateService, ILogger<FleetComplianceTemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<FleetComplianceTemplateDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        try
        {
            var templates = await _templateService.GetTemplatesAsync(includeInactive);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet compliance templates");
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetComplianceTemplateDto>> GetById(Guid id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null) return NotFound();
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet compliance template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetComplianceTemplateDto>> Create([FromBody] CreateFleetComplianceTemplateDto dto)
    {
        try
        {
            var created = await _templateService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet compliance template");
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetComplianceTemplateDto>> Update(Guid id, [FromBody] UpdateFleetComplianceTemplateDto dto)
    {
        try
        {
            var updated = await _templateService.UpdateTemplateAsync(id, dto);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet compliance template {Id}", id);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _templateService.DeleteTemplateAsync(id);
            return Ok(new { success = ok });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet compliance template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }
}

