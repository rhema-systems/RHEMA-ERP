using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The organisation-unit change log: who a unit reported to, who headed it, and when each changed.
/// </summary>
/// <remarks>
/// Read-only. Rows are written by <c>OrganizationUnitService</c> as a side effect of a restructure
/// or a change of head — there is no endpoint that creates one directly, because an audit trail
/// somebody can author by hand is not one.
///
/// Gated on the HR/admin roles rather than the bare <c>[Authorize]</c> it carried: the log names the
/// employees who have led each unit, which is org-structure information about identifiable people.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = OrganizationUnitHistoryController.HrRoles)]
public class OrganizationUnitHistoryController : ControllerBase
{
    internal const string HrRoles =
        Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin + "," + Constants.Roles.Hr;

    private readonly IOrganizationUnitHistoryService _historyService;
    private readonly ILogger<OrganizationUnitHistoryController> _logger;

    public OrganizationUnitHistoryController(
        IOrganizationUnitHistoryService historyService,
        ILogger<OrganizationUnitHistoryController> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves organization unit history with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<OrganizationUnitHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _historyService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged organization unit history");
            return StatusCode(500, "An error occurred while retrieving organization unit history");
        }
    }

    /// <summary>
    /// Retrieves organization unit history by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationUnitHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _historyService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization unit history with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization unit history");
        }
    }

    /// <summary>
    /// Retrieves all history records for a specific unit
    /// </summary>
    [HttpGet("unit/{unitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUnitId(Guid unitId)
    {
        try
        {
            var response = await _historyService.GetByUnitIdAsync(unitId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history for organization unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while retrieving organization unit history");
        }
    }

    /// <summary>
    /// Retrieves history records within a date range
    /// </summary>
    [HttpGet("date-range")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        try
        {
            var response = await _historyService.GetByDateRangeAsync(startDate, endDate);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization unit history for date range {StartDate} to {EndDate}", startDate, endDate);
            return StatusCode(500, "An error occurred while retrieving organization unit history");
        }
    }

    /// <summary>
    /// Retrieves latest history record for a unit
    /// </summary>
    [HttpGet("unit/{unitId:guid}/latest")]
    [ProducesResponseType(typeof(OrganizationUnitHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestByUnitId(Guid unitId)
    {
        try
        {
            var response = await _historyService.GetLatestByUnitIdAsync(unitId);
            if (response == null)
                return NotFound(new { message = "No history found for this unit" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest history for organization unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while retrieving organization unit history");
        }
    }

    /// <summary>
    /// Retrieves active history record for a unit
    /// </summary>
    [HttpGet("unit/{unitId:guid}/active")]
    [ProducesResponseType(typeof(OrganizationUnitHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveHistory(Guid unitId)
    {
        try
        {
            var response = await _historyService.GetActiveHistoryAsync(unitId);
            if (response == null)
                return NotFound(new { message = "No active history found for this unit" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active history for organization unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while retrieving organization unit history");
        }
    }
}
