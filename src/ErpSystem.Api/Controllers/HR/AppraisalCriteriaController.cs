using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalCompetencyController : ControllerBase
{
    private readonly IAppraisalCompetencyService _appraisalCompetencyService;
    private readonly ILogger<AppraisalCompetencyController> _logger;

    public AppraisalCompetencyController(IAppraisalCompetencyService appraisalCompetencyService, ILogger<AppraisalCompetencyController> logger)
    {
        _appraisalCompetencyService = appraisalCompetencyService;
        _logger = logger;
    }

    /// <summary>A rule refused the write (performance closure E-g1): answered 422 with the reason.</summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Appraisal competency rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// Get all appraisal competencies
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCompetencyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _appraisalCompetencyService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all appraisal competencies");
            return StatusCode(500, "An error occurred while retrieving appraisal competencies");
        }
    }

    /// <summary>
    /// Get appraisal competencies with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalCompetencyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _appraisalCompetencyService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal competencies");
            return StatusCode(500, "An error occurred while retrieving appraisal competencies");
        }
    }

    /// <summary>
    /// Get appraisal competency by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCompetencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _appraisalCompetencyService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal competency with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal competency");
        }
    }

    /// <summary>
    /// Create a new appraisal competency
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalCompetencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalCompetencyDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalCompetencyService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal competency");
            return StatusCode(500, "An error occurred while creating the appraisal competency");
        }
    }

    /// <summary>
    /// Update an existing appraisal competency
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCompetencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalCompetencyDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalCompetencyService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal competency with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the appraisal competency");
        }
    }

    /// <summary>
    /// Delete an appraisal competency
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _appraisalCompetencyService.DeleteAsync(id);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "deleting");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal competency with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal competency");
        }
    }
}
