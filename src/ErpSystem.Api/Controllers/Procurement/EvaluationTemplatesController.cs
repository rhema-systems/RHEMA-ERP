using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class EvaluationTemplatesController : ControllerBase
{
    private readonly IEvaluationTemplateService _service;
    private readonly ILogger<EvaluationTemplatesController> _logger;

    public EvaluationTemplatesController(
        IEvaluationTemplateService service,
        ILogger<EvaluationTemplatesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all evaluation templates
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationTemplateDto>>> GetAll()
    {
        try
        {
            var templates = await _service.GetAllAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation templates");
            return StatusCode(500, "An error occurred while retrieving evaluation templates");
        }
    }

    /// <summary>
    /// Get active evaluation templates
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationTemplateDto>>> GetActive()
    {
        try
        {
            var templates = await _service.GetActiveAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active evaluation templates");
            return StatusCode(500, "An error occurred while retrieving active evaluation templates");
        }
    }

    /// <summary>
    /// Get active evaluation templates for dropdown
    /// </summary>
    [HttpGet("dropdown")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationTemplateListItemDto>>> GetForDropdown(
        [FromQuery] string? category = null,
        [FromQuery] string? tenderType = null)
    {
        try
        {
            var templates = await _service.GetActiveForDropdownAsync(category, tenderType);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation templates for dropdown");
            return StatusCode(500, "An error occurred while retrieving evaluation templates");
        }
    }

    /// <summary>
    /// Get evaluation template by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<EvaluationTemplateDto>> GetById(Guid id)
    {
        try
        {
            var template = await _service.GetByIdWithCriteriaAsync(id);
            if (template == null)
            {
                return NotFound($"Evaluation template with ID '{id}' not found");
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the evaluation template");
        }
    }

    /// <summary>
    /// Get evaluation templates by category
    /// </summary>
    [HttpGet("category/{category}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationTemplateDto>>> GetByCategory(string category)
    {
        try
        {
            var templates = await _service.GetByCategoryAsync(category);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation templates for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving evaluation templates");
        }
    }

    /// <summary>
    /// Get evaluation templates by tender type
    /// </summary>
    [HttpGet("tender-type/{tenderType}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationTemplateDto>>> GetByTenderType(string tenderType)
    {
        try
        {
            var templates = await _service.GetByTenderTypeAsync(tenderType);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation templates for tender type {TenderType}", tenderType);
            return StatusCode(500, "An error occurred while retrieving evaluation templates");
        }
    }

    /// <summary>
    /// Get default evaluation template for category and tender type
    /// </summary>
    [HttpGet("default")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<EvaluationTemplateDto>> GetDefault([FromQuery] string category, [FromQuery] string tenderType)
    {
        try
        {
            var template = await _service.GetDefaultAsync(category, tenderType);
            if (template == null)
            {
                return NotFound($"No default evaluation template found for category '{category}' and tender type '{tenderType}'");
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default evaluation template");
            return StatusCode(500, "An error occurred while retrieving the default evaluation template");
        }
    }

    /// <summary>
    /// Create evaluation template
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<EvaluationTemplateDto>> Create([FromBody] CreateEvaluationTemplateDto dto)
    {
        try
        {
            var template = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating evaluation template");
            return StatusCode(500, "An error occurred while creating the evaluation template");
        }
    }

    /// <summary>
    /// Update evaluation template
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<EvaluationTemplateDto>> Update(Guid id, [FromBody] UpdateEvaluationTemplateDto dto)
    {
        try
        {
            var template = await _service.UpdateAsync(id, dto);
            return Ok(template);
        }
        catch (TenderEvaluationConfigurationException configurationError)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = 422, Title = "Evaluation configuration needs correction", Detail = configurationError.Message,
                Extensions = { ["code"] = configurationError.Code }
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation template {Id}", id);
            return StatusCode(500, "An error occurred while updating the evaluation template");
        }
    }

    /// <summary>
    /// Delete evaluation template
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (TenderEvaluationConfigurationException configurationError)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = 422, Title = "Evaluation configuration needs correction", Detail = configurationError.Message,
                Extensions = { ["code"] = configurationError.Code }
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluation template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the evaluation template");
        }
    }

    /// <summary>
    /// Validate that criteria weights sum to 100
    /// </summary>
    [HttpGet("{id}/validate-weights")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<bool>> ValidateWeights(Guid id)
    {
        try
        {
            var isValid = await _service.ValidateCriteriaWeightsAsync(id);
            return Ok(new { isValid, message = isValid ? "Weights are valid" : "Weights must sum to 100" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating evaluation template weights {Id}", id);
            return StatusCode(500, "An error occurred while validating the evaluation template");
        }
    }
}

