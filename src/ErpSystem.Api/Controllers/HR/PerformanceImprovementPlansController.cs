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
public class PerformanceImprovementPlansController : ControllerBase
{
    private readonly IPerformanceImprovementPlanService _improvementPlanService;
    private readonly ILogger<PerformanceAppraisalsController> _logger;

    public PerformanceImprovementPlansController(IPerformanceImprovementPlanService improvementPlanService, ILogger<PerformanceAppraisalsController> logger)
    {
        _improvementPlanService = improvementPlanService;
        _logger = logger;
    }

    /// <summary>
    /// Get all performance improvement plans
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _improvementPlanService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIPs with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _improvementPlanService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIP by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _improvementPlanService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plan with Id {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the performance improvement plan");
        }
    }

    /// <summary>
    /// Get PIPs by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId)
    {
        try
        {
            var response = await _improvementPlanService.GetByEmployeeIdAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIPs by status
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(PipStatus status)
    {
        try
        {
            var response = await _improvementPlanService.GetByStatusAsync(status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get all active PIPs
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive()
    {
        try
        {
            var response = await _improvementPlanService.GetActivePipsAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving active performance improvement plans");
        }
    }

    /// <summary>
    /// Create a new PIP
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePerformanceImprovementPlanDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance improvement plan");
            return StatusCode(500, "An error occurred while creating the performance improvement plan");
        }
    }

    /// <summary>
    /// Update an existing PIP
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceImprovementPlanDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while updating the performance improvement plan");
        }
    }

    /// <summary>
    /// Update PIP status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePipStatusDto statusDto)
    {
        try
        {
            if (id != statusDto.PipId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.UpdateStatusAsync(statusDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance improvement plan with ID {PipId}", id);
            return StatusCode(500, "An error occurred while updating the performance improvement plan");
        }
    }

    /// <summary>
    /// Complete a PIP
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompletePipDto completeDto)
    {
        try
        {
            if (id != completeDto.PipId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.CompletePipAsync(completeDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while completing the performance improvement plan");
        }
    }

    /// <summary>
    /// Delete a PIP
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _improvementPlanService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while deleting the performance improvement plan");
        }
    }
}
