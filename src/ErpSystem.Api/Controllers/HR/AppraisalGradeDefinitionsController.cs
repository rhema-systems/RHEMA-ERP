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
public class AppraisalGradeDefinitionsController : ControllerBase
{
    private readonly IAppraisalGradeDefinitionService _gradeDefinitionService;
    private readonly ILogger<AppraisalGradeDefinitionsController> _logger;

    public AppraisalGradeDefinitionsController(IAppraisalGradeDefinitionService gradeDefinitionService, ILogger<AppraisalGradeDefinitionsController> logger)
    {
        _gradeDefinitionService = gradeDefinitionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all appraisal grade definitions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalGradeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _gradeDefinitionService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all appraisal grade definitions");
            return StatusCode(500, "An error occurred while retrieving appraisal grade definitions");
        }
    }

    /// <summary>
    /// Get appraisal grade definitions with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalGradeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _gradeDefinitionService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal grade definitions");
            return StatusCode(500, "An error occurred while retrieving appraisal grade definitions");
        }
    }

    /// <summary>
    /// Get appraisal grade definition by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalGradeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _gradeDefinitionService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal grade definition with Id {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal grade definition");
        }
    }

    /// <summary>
    /// Create a new appraisal grade definition
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalGradeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalGradeDefinitionDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _gradeDefinitionService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal grade definition");
            return StatusCode(500, "An error occurred while creating the appraisal grade definition");
        }
    }

    /// <summary>
    /// Update an existing appraisal grade definition
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalGradeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalGradeDefinitionDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _gradeDefinitionService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal grade definition with Id {GradeDefinitionId}", id);
            return StatusCode(500, "An error occurred while updating the appraisal grade definition");
        }
    }

    /// <summary>
    /// Delete an appraisal grade definition
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _gradeDefinitionService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal grade definition with Id {GradeDefinitionId}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal grade definition");
        }
    }
}
