using ErpSystem.Application.HR.Extensions;
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

    /// <summary>Largest page this endpoint will serve, however large a page the caller asks for.</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// The change-log register: every recorded change, newest first, optionally narrowed to one unit,
    /// a date range, or a kind of change.
    /// </summary>
    /// <remarks>
    /// The four filters arrived in areas 19-23 slice 5 with the register that uses them. An
    /// unrecognised <paramref name="changeType"/> is <b>refused</b> rather than ignored: a filter
    /// that silently does nothing reads, from the screen, exactly like a filter that matched
    /// everything.
    /// </remarks>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<OrganizationUnitHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? unitId = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] string? changeType = null)
    {
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            return BadRequest(new { message = "The start date cannot be after the end date." });

        var filter = new OrganizationUnitHistoryFilterDto
        {
            UnitId = unitId,
            StartDate = startDate,
            EndDate = endDate,
        };

        if (!string.IsNullOrWhiteSpace(changeType))
        {
            if (!OrganizationUnitChangeTypes.TryResolve(changeType, out var resolved))
                return BadRequest(new
                {
                    message = $"Unknown change type '{changeType}'. Expected one of: {string.Join(", ", OrganizationUnitChangeTypes.All)}.",
                });

            filter.ChangeType = resolved;
        }

        try
        {
            var response = await _historyService.GetPagedAsync(
                pageNumber < 1 ? 1 : pageNumber,
                pageSize < 1 ? 1 : Math.Min(pageSize, MaxPageSize),
                filter);
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        if (startDate > endDate)
            return BadRequest(new { message = "The start date cannot be after the end date." });

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
