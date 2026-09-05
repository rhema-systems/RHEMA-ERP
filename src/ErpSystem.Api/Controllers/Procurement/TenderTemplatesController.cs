using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class TenderTemplatesController : ControllerBase
{
    private readonly ITenderTemplateService _templateService;
    private readonly ILogger<TenderTemplatesController> _logger;

    public TenderTemplatesController(
        ITenderTemplateService templateService,
        ILogger<TenderTemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all templates
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderTemplateDto>>> GetTemplates()
    {
        try
        {
            // TODO: Service interface doesn't have GetTemplatesAsync - using GetActiveTemplatesAsync instead
            var templates = await _templateService.GetActiveTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates");
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    /// <summary>
    /// Get active templates
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderTemplateDto>>> GetActiveTemplates()
    {
        try
        {
            var templates = await _templateService.GetActiveTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active templates");
            return StatusCode(500, "An error occurred while retrieving active templates");
        }
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<TenderTemplateDto>> GetTemplate(Guid id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template {TemplateId}", id);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    /// <summary>
    /// Create template
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderTemplateDto>> CreateTemplate([FromBody] CreateTenderTemplateDto dto)
    {
        try
        {
            var template = await _templateService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    /// <summary>
    /// Update template
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateTenderTemplateDto dto)
    {
        try
        {
            // Service interface expects CreateTenderTemplateDto, not UpdateTenderTemplateDto
            // Map UpdateTenderTemplateDto to CreateTenderTemplateDto
            var createDto = new CreateTenderTemplateDto
            {
                TemplateName = dto.TemplateName,
                Description = dto.Description,
                TenderType = dto.TenderType,
                Category = dto.Category,
                PriceWeightage = dto.PriceWeightage,
                QualityWeightage = dto.QualityWeightage,
                DeliveryWeightage = dto.DeliveryWeightage,
                ExperienceWeightage = dto.ExperienceWeightage,
                EvaluationCriteriaJson = dto.EvaluationCriteriaJson,
                DefaultValidityDays = dto.DefaultValidityDays,
                RequiredDocuments = dto.RequiredDocuments,
                TermsAndConditions = dto.TermsAndConditions,
                RequiresPrequalification = dto.RequiresPrequalification,
                AllowPartialBids = dto.AllowPartialBids,
                IsActive = dto.IsActive
            };

            var template = await _templateService.UpdateTemplateAsync(id, createDto);
            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    /// <summary>
    /// Delete template
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }

    /// <summary>
    /// Create tender from template
    /// </summary>
    [HttpPost("{id}/create-tender")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderDto>> CreateTenderFromTemplate(Guid id, [FromBody] CreateTenderFromTemplateDto dto)
    {
        try
        {
            // Service interface expects (Guid templateId, string title), not CreateTenderFromTemplateDto
            var tender = await _templateService.CreateTenderFromTemplateAsync(id, dto.Title);
            return Ok(tender);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender from template {TemplateId}", id);
            return StatusCode(500, "An error occurred while creating tender from template");
        }
    }
}
