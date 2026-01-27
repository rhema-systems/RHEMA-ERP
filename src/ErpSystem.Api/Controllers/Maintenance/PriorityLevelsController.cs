using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/priority-levels")]
[AllowAnonymous] // Allow access without authentication for now
public class PriorityLevelsController : ControllerBase
{
    private readonly IPriorityLevelService _priorityLevelService;
    private readonly ILogger<PriorityLevelsController> _logger;

    public PriorityLevelsController(
        IPriorityLevelService priorityLevelService,
        ILogger<PriorityLevelsController> logger)
    {
        _priorityLevelService = priorityLevelService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of priority levels with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PriorityLevelDto>>> GetPriorityLevels(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var filter = new PriorityLevelFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                IsActive = isActive
            };

            var result = await _priorityLevelService.GetPriorityLevelsPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving priority levels");
            return StatusCode(500, "An error occurred while retrieving priority levels");
        }
    }

    /// <summary>
    /// Gets all active priority levels
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<PriorityLevelDto>>> GetActivePriorityLevels()
    {
        try
        {
            var result = await _priorityLevelService.GetActivePriorityLevelsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active priority levels");
            return StatusCode(500, "An error occurred while retrieving active priority levels");
        }
    }

    /// <summary>
    /// Gets a specific priority level by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PriorityLevelDto>> GetPriorityLevel(Guid id)
    {
        try
        {
            var priorityLevel = await _priorityLevelService.GetPriorityLevelByIdAsync(id);
            if (priorityLevel == null)
            {
                return NotFound($"Priority level with ID {id} not found");
            }

            return Ok(priorityLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving priority level {PriorityLevelId}", id);
            return StatusCode(500, "An error occurred while retrieving the priority level");
        }
    }

    /// <summary>
    /// Creates a new priority level
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PriorityLevelDto>> CreatePriorityLevel([FromBody] CreatePriorityLevelDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var priorityLevel = await _priorityLevelService.CreatePriorityLevelAsync(createDto);
            return CreatedAtAction(nameof(GetPriorityLevel), new { id = priorityLevel.Id }, priorityLevel);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating priority level");
            return StatusCode(500, "An error occurred while creating the priority level");
        }
    }

    /// <summary>
    /// Updates an existing priority level
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PriorityLevelDto>> UpdatePriorityLevel(Guid id, [FromBody] UpdatePriorityLevelDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var priorityLevel = await _priorityLevelService.UpdatePriorityLevelAsync(id, updateDto);
            return Ok(priorityLevel);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating priority level {PriorityLevelId}", id);
            return StatusCode(500, "An error occurred while updating the priority level");
        }
    }

    /// <summary>
    /// Deletes a priority level
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePriorityLevel(Guid id)
    {
        try
        {
            await _priorityLevelService.DeletePriorityLevelAsync(id);
            return NoContent();
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
            _logger.LogError(ex, "Error deleting priority level {PriorityLevelId}", id);
            return StatusCode(500, "An error occurred while deleting the priority level");
        }
    }

    /// <summary>
    /// Toggles the active status of a priority level
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<PriorityLevelDto>> ToggleStatus(Guid id)
    {
        try
        {
            var priorityLevel = await _priorityLevelService.TogglePriorityLevelStatusAsync(id);
            return Ok(priorityLevel);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling priority level status {PriorityLevelId}", id);
            return StatusCode(500, "An error occurred while toggling the priority level status");
        }
    }
}
