using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceAppraisalsController : ControllerBase
{
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly ILogger<PerformanceAppraisalsController> _logger;

    public PerformanceAppraisalsController(IPerformanceAppraisalService appraisalService, ILogger<PerformanceAppraisalsController> logger)
    {
        _appraisalService = appraisalService;
        _logger = logger;
    }

    /// <summary>
    /// Get all performance appraisals
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _appraisalService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _appraisalService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisal by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _appraisalService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisal with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the performance appraisal");
        }
    }

    /// <summary>
    /// Get performance appraisals by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId)
    {
        try
        {
            var response = await _appraisalService.GetByEmployeeIdAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by year
    /// </summary>
    [HttpGet("year/{year}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByYear(int year)
    {
        try
        {
            var response = await _appraisalService.GetByYearAsync(year);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by status
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(AppraisalStatus status)
    {
        try
        {
            var response = await _appraisalService.GetByStatusAsync(status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Create a new performance appraisal
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePerformanceAppraisalDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance appraisal");
            return StatusCode(500, "An error occurred while creating the performance appraisal");
        }
    }

    /// <summary>
    /// Update an existing performance appraisal
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceAppraisalDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// Update appraisal status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppraisalStatusDto statusDto)
    {
        try
        {
            if (id != statusDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateStatusAsync(statusDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// Calculate overall score for an appraisal
    /// </summary>
    [HttpPost("{id:guid}/calculate-score")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CalculateOverallScore(Guid id)
    {
        try
        {
            var response = await _appraisalService.CalculateOverallScoreAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating overall score for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while calculating the overall score for the appraisal");
        }
    }

    /// <summary>
    /// File an appeal for an appraisal
    /// </summary>
    [HttpPost("{id:guid}/appeal")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FileAppeal(Guid id, [FromBody] FileAppraisalAppealDto appealDto)
    {
        try
        {
            if (id != appealDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.FileAppealAsync(appealDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filing appraisal appeal for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while filing appraisal appeal");
        }
    }

    /// <summary>
    /// Resolve an appraisal appeal
    /// </summary>
    [HttpPost("{id:guid}/appeal/resolve")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveAppeal(Guid id, [FromBody] ResolveAppraisalAppealDto resolveDto)
    {
        try
        {
            if (id != resolveDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.ResolveAppealAsync(resolveDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving appraisal appeal for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while resolving appraisal appeal");
        }
    }

    /// <summary>
    /// Delete a performance appraisal
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _appraisalService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while deleting the performance appraisal");
        }
    }
}
