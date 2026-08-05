using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoalLibraryController : ControllerBase
{
    private readonly IGoalLibraryService _goalLibraryService;
    private readonly ILogger<GoalLibraryController> _logger;

    public GoalLibraryController(IGoalLibraryService goalLibraryService, ILogger<GoalLibraryController> logger)
    {
        _goalLibraryService = goalLibraryService;
        _logger = logger;
    }

    /// <summary>Get all goal library items</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GoalLibraryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal library items");
            return StatusCode(500, "An error occurred while retrieving goal library items");
        }
    }

    /// <summary>Get goal library items with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<GoalLibraryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged goal library items");
            return StatusCode(500, "An error occurred while retrieving goal library items");
        }
    }

    /// <summary>Get goal library item by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GoalLibraryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal library item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the goal library item");
        }
    }

    /// <summary>Get goal library items by position</summary>
    [HttpGet("by-position/{positionId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<GoalLibraryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPositionId(Guid positionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetByPositionIdAsync(positionId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal library items for position {PositionId}", positionId);
            return StatusCode(500, "An error occurred while retrieving goal library items");
        }
    }

    /// <summary>Get goal library items by organisation unit</summary>
    [HttpGet("by-org-unit/{orgUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<GoalLibraryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrganizationUnitId(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetByOrganizationUnitIdAsync(orgUnitId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal library items for org unit {OrgUnitId}", orgUnitId);
            return StatusCode(500, "An error occurred while retrieving goal library items");
        }
    }

    /// <summary>Get all active goal library items</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<GoalLibraryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveItems(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetActiveItemsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active goal library items");
            return StatusCode(500, "An error occurred while retrieving active goal library items");
        }
    }

    /// <summary>Create a new goal library item</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GoalLibraryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateGoalLibraryDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating goal library item");
            return StatusCode(500, "An error occurred while creating the goal library item");
        }
    }

    /// <summary>Update an existing goal library item</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GoalLibraryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoalLibraryDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal library item {Id}", id);
            return StatusCode(500, "An error occurred while updating the goal library item");
        }
    }

    /// <summary>Delete a goal library item</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Goal library item not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal library item {Id}", id);
            return StatusCode(500, "An error occurred while deleting the goal library item");
        }
    }

    /// <summary>Get the number of employee goals that reference this library item</summary>
    [HttpGet("{id:guid}/usage-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageCount(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _goalLibraryService.GetEmployeeGoalUsageCountAsync(id, cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage count for goal library item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the usage count");
        }
    }

    /// <summary>Set the active status of a goal library item</summary>
    [HttpPatch("{id:guid}/active-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActiveStatus(Guid id, [FromBody] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.SetActiveStatusAsync(id, isActive, cancellationToken);
            if (!result) return NotFound(new { message = "Goal library item not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating active status for goal library item {Id}", id);
            return StatusCode(500, "An error occurred while updating the goal library item");
        }
    }

    /// <summary>
    /// Selector endpoint — returns paged, projected goal library items for use in the selector modal.
    /// Supports search (title, description) and optional scope filters.
    /// </summary>
    [HttpGet("selector")]
    [ProducesResponseType(typeof(PagedResult<GoalLibrarySelectorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelector(
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true,
        [FromQuery] Guid? organizationLevelId = null,
        [FromQuery] Guid? organizationUnitId = null,
        [FromQuery] Guid? positionId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetSelectorPagedAsync(
                search, activeOnly, organizationLevelId, organizationUnitId, positionId,
                page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal library selector data");
            return StatusCode(500, "An error occurred while retrieving goal library templates");
        }
    }

    /// <summary>Get goal library item details including usage statistics</summary>
    [HttpGet("{id:guid}/details")]
    [ProducesResponseType(typeof(GoalLibraryDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await _goalLibraryService.GetDetailsAsync(id, cancellationToken);
            return Ok(details);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving details for goal library item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the goal library details");
        }
    }

    /// <summary>Get usage statistics for a goal library item</summary>
    [HttpGet("{id:guid}/usage-stats")]
    [ProducesResponseType(typeof(GoalLibraryUsageStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageStats(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _goalLibraryService.GetUsageStatsAsync(id, cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage stats for goal library item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving usage statistics");
        }
    }

    /// <summary>Get paged usage rows for a goal library item</summary>
    [HttpGet("{id:guid}/usage")]
    [ProducesResponseType(typeof(PagedResult<GoalLibraryUsageRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsagePaged(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalLibraryService.GetUsagePagedAsync(id, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage rows for goal library item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving usage data");
        }
    }
}
