using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class EvaluationCriteriaController : ControllerBase
{
    private readonly IEvaluationCriterionService _service;
    private readonly ILogger<EvaluationCriteriaController> _logger;

    public EvaluationCriteriaController(
        IEvaluationCriterionService service,
        ILogger<EvaluationCriteriaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all evaluation criteria
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationCriterionDto>>> GetAll()
    {
        try
        {
            var criteria = await _service.GetAllAsync();
            return Ok(criteria);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation criteria");
            return StatusCode(500, "An error occurred while retrieving evaluation criteria");
        }
    }

    /// <summary>
    /// Get active evaluation criteria
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationCriterionDto>>> GetActive()
    {
        try
        {
            var criteria = await _service.GetActiveAsync();
            return Ok(criteria);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active evaluation criteria");
            return StatusCode(500, "An error occurred while retrieving active evaluation criteria");
        }
    }

    /// <summary>
    /// Get evaluation criterion by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<EvaluationCriterionDto>> GetById(Guid id)
    {
        try
        {
            var criterion = await _service.GetByIdAsync(id);
            if (criterion == null)
            {
                return NotFound($"Evaluation criterion with ID '{id}' not found");
            }
            return Ok(criterion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation criterion {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the evaluation criterion");
        }
    }

    /// <summary>
    /// Get evaluation criteria by category
    /// </summary>
    [HttpGet("category/{category}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<EvaluationCriterionDto>>> GetByCategory(string category)
    {
        try
        {
            var criteria = await _service.GetByCategoryAsync(category);
            return Ok(criteria);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation criteria for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving evaluation criteria");
        }
    }

    /// <summary>
    /// Create evaluation criterion
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<EvaluationCriterionDto>> Create([FromBody] CreateEvaluationCriterionDto dto)
    {
        try
        {
            var criterion = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = criterion.Id }, criterion);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating evaluation criterion");
            return StatusCode(500, "An error occurred while creating the evaluation criterion");
        }
    }

    /// <summary>
    /// Update evaluation criterion
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<EvaluationCriterionDto>> Update(Guid id, [FromBody] UpdateEvaluationCriterionDto dto)
    {
        try
        {
            var criterion = await _service.UpdateAsync(id, dto);
            return Ok(criterion);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation criterion {Id}", id);
            return StatusCode(500, "An error occurred while updating the evaluation criterion");
        }
    }

    /// <summary>
    /// Delete evaluation criterion
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
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluation criterion {Id}", id);
            return StatusCode(500, "An error occurred while deleting the evaluation criterion");
        }
    }
}

