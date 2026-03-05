using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalCriteriaController : ControllerBase
{
    private readonly IAppraisalCriteriaService _appraisalCriteriaService;
    private readonly ILogger<AppraisalCriteriaController> _logger;

    public AppraisalCriteriaController(IAppraisalCriteriaService appraisalCriteriaService, ILogger<AppraisalCriteriaController> logger)
    {
        _appraisalCriteriaService = appraisalCriteriaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all appraisal criteria
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCriteriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _appraisalCriteriaService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all appraisal criteria");
            return StatusCode(500, "An error occurred while retrieving appraisal criteria");
        }
    }

    /// <summary>
    /// Get appraisal criteria with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalCriteriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _appraisalCriteriaService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal criteria");
            return StatusCode(500, "An error occurred while retrieving appraisal criteria");
        }
    }

    /// <summary>
    /// Get appraisal criteria by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCriteriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _appraisalCriteriaService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal criteria with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal criteria");
        }
    }

    /// <summary>
    /// Create a new appraisal criteria
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalCriteriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalCriteriaDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalCriteriaService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal criteria");
            return StatusCode(500, "An error occurred while creating the appraisal criteria");
        }
    }

    /// <summary>
    /// Update an existing appraisal criteria
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCriteriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalCriteriaDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalCriteriaService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal criteria with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the appraisal criteria");
        }
    }

    /// <summary>
    /// Delete a appraisal criteria
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _appraisalCriteriaService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal criteria with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal criteria");
        }
    }
}
