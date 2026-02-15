using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/inspection-templates")]
[Authorize]
public class InspectionTemplatesController : ControllerBase
{
    private readonly IInspectionTemplateService _service;
    private readonly ILogger<InspectionTemplatesController> _logger;

    public InspectionTemplatesController(IInspectionTemplateService service, ILogger<InspectionTemplatesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InspectionTemplateDto>>> GetAll([FromQuery] bool activeOnly = false, [FromQuery] string? category = null)
    {
        try
        {
            if (activeOnly) return Ok(await _service.GetActiveTemplatesAsync());
            if (!string.IsNullOrWhiteSpace(category)) return Ok(await _service.GetTemplatesByCategoryAsync(category));
            return Ok(await _service.GetAllTemplatesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection templates");
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionTemplateDto>> GetById(Guid id)
    {
        try
        {
            var item = await _service.GetTemplateByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    [HttpPost]
    public async Task<ActionResult<InspectionTemplateDto>> Create([FromBody] CreateInspectionTemplateDto dto)
    {
        try
        {
            var created = await _service.CreateTemplateAsync(dto);
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
            _logger.LogError(ex, "Error creating inspection template");
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InspectionTemplateDto>> Update(Guid id, [FromBody] UpdateInspectionTemplateDto dto)
    {
        try
        {
            var updated = await _service.UpdateTemplateAsync(id, dto);
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
            _logger.LogError(ex, "Error updating inspection template {Id}", id);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteTemplateAsync(id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }
}

