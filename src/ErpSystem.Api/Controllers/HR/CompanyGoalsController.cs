using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class CompanyGoalsController : ControllerBase
{
    private readonly ICompanyGoalService _companyGoalService;
    private readonly ILogger<CompanyGoalsController> _logger;

    public CompanyGoalsController(ICompanyGoalService companyGoalService, ILogger<CompanyGoalsController> logger)
    {
        _companyGoalService = companyGoalService;
        _logger = logger;
    }

    /// <summary>Get company goals with pagination, optionally filtered by cycle</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<CompanyGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetPagedAsync(pageNumber, pageSize, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged company goals");
            return StatusCode(500, "An error occurred while retrieving company goals");
        }
    }

    /// <summary>Get company goals by cycle</summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CompanyGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycleId(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetByCycleIdAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company goals for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving company goals");
        }
    }

    /// <summary>Get visible company goals for a cycle</summary>
    [HttpGet("visible/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CompanyGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisible(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetVisibleGoalsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visible company goals for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving visible company goals");
        }
    }

    /// <summary>Get a company goal by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the company goal");
        }
    }

    /// <summary>Create a new company goal</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyGoalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyGoalDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company goal");
            return StatusCode(500, "An error occurred while creating the company goal");
        }
    }

    /// <summary>Update an existing company goal</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service updates the body's id, so without this a PUT to one goal's URL could edit another.
        if (id != updateDto.Id)
            return BadRequest(new { message = "Route id does not match body id." });

        try
        {
            var result = await _companyGoalService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the company goal");
        }
    }

    /// <summary>Delete a company goal</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Company goal not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company goal {Id}", id);
            return StatusCode(500, "An error occurred while deleting the company goal");
        }
    }

    /// <summary>Set visibility for a company goal</summary>
    [HttpPatch("{id:guid}/visibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVisibility(Guid id, [FromBody] bool isVisible, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.SetVisibilityAsync(id, isVisible, cancellationToken);
            if (!result) return NotFound(new { message = "Company goal not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting visibility for company goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the company goal");
        }
    }

    /// <summary>Get cascade statistics for a company goal</summary>
    [HttpGet("{id:guid}/cascade-stats")]
    [ProducesResponseType(typeof(CompanyGoalCascadeStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCascadeStats(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetCascadeStatsAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cascade stats for company goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving cascade statistics");
        }
    }

    /// <summary>
    /// Strategy-dashboard projection list with cascade counts.
    /// Requires cycleId. Supports search, priority, visibility and due-date filters.
    /// </summary>
    [HttpGet("dashboard/paged")]
    [Authorize(Roles = Constants.Roles.Hr + ",Admin," + Constants.Roles.SuperAdmin)]
    [ProducesResponseType(typeof(PagedResult<CompanyGoalListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardPaged(
        [FromQuery] Guid cycleId,
        [FromQuery] string? search = null,
        [FromQuery] GoalPriority? priority = null,
        [FromQuery] bool? isVisible = null,
        [FromQuery] DateOnly? dueDateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetDashboardPagedAsync(
                cycleId, search, priority, isVisible, dueDateTo,
                pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company goal dashboard page for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving company goals");
        }
    }

    /// <summary>
    /// Aggregated header-card metrics for the strategy dashboard.
    /// Single projection query — does not load navigation collections.
    /// </summary>
    [HttpGet("dashboard/metrics")]
    [Authorize(Roles = Constants.Roles.Hr + ",Admin," + Constants.Roles.SuperAdmin)]
    [ProducesResponseType(typeof(CompanyGoalDashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardMetrics(
        [FromQuery] Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _companyGoalService.GetDashboardMetricsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company goal metrics for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving dashboard metrics");
        }
    }
}
