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
public class StrategicGoalsController : ControllerBase
{
    private readonly IStrategicGoalService _service;
    private readonly ILogger<StrategicGoalsController> _logger;

    public StrategicGoalsController(IStrategicGoalService service, ILogger<StrategicGoalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get all strategic goals (optionally active only)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StrategicGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetAllAsync(activeOnly, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic goals");
            return StatusCode(500, "An error occurred while retrieving strategic goals");
        }
    }

    /// <summary>Get strategic goals with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<StrategicGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged strategic goals");
            return StatusCode(500, "An error occurred while retrieving strategic goals");
        }
    }

    /// <summary>Get a strategic goal by id</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StrategicGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the strategic goal");
        }
    }

    /// <summary>Create a strategic goal</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StrategicGoalDto), StatusCodes.Status201Created)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateStrategicGoalDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating strategic goal");
            return StatusCode(500, "An error occurred while creating the strategic goal");
        }
    }

    /// <summary>Update a strategic goal</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StrategicGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStrategicGoalDto dto, CancellationToken cancellationToken = default)
    {
        if (id != dto.Id)
            return BadRequest(new { message = "Route id does not match body id." });

        try
        {
            var result = await _service.UpdateAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating strategic goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the strategic goal");
        }
    }

    /// <summary>Delete a strategic goal</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting strategic goal {Id}", id);
            return StatusCode(500, "An error occurred while deleting the strategic goal");
        }
    }

    /// <summary>Activate or deactivate a strategic goal</summary>
    [HttpPatch("{id:guid}/active-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> SetActiveStatus(Guid id, [FromBody] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            await _service.SetActiveStatusAsync(id, isActive, cancellationToken);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active status for strategic goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the strategic goal");
        }
    }
}
