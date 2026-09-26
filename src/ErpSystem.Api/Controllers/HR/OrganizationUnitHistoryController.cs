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
/// Rows are written by <c>OrganizationUnitService</c> as a side effect of a creation, a
/// restructure or a change of head. Two admin-tier writes exist beside those since demo feedback
/// round 2 (O-3b): a hand-recorded entry (moves nothing, appoints nobody, classifies as
/// <c>Other</c>, must give a reason) and a correction of a row's dates, reason and notes. Neither
/// can rewrite WHAT a row says changed, and there is still no delete — an audit trail somebody can
/// author freely is not one, and these two doors are the narrowest that still let the user record
/// a restructure with the date it really took effect.
///
/// Gated because the log names the employees who have led each unit, which is org-structure
/// information about identifiable people. W3 slice 13 converted the role gate to
/// HR.Employee.Read (the foundation family, same reach) — a census gap: slice 11 swept the
/// org-structure registers and missed this one.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
public class OrganizationUnitHistoryController : ControllerBase
{
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
    /// Records an entry by hand. Admin-tier; a reason is required.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(OrganizationUnitHistoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationUnitHistoryDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _historyService.CreateManualEntryAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault — the same contract as the unit controller.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording an organization unit history entry");
            return StatusCode(500, "An error occurred while recording the organization unit history entry");
        }
    }

    /// <summary>
    /// Corrects a row's dates, reason and notes. Admin-tier. The ids that say what changed are not
    /// on the body and cannot be altered.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(OrganizationUnitHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationUnitHistoryDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (id != dto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });

        try
        {
            var response = await _historyService.UpdateEntryAsync(dto);
            return Ok(response);
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
            _logger.LogError(ex, "Error correcting organization unit history entry {Id}", id);
            return StatusCode(500, "An error occurred while correcting the organization unit history entry");
        }
    }

    // ⚠ `unit/{id}/latest` and `unit/{id}/active` were deleted in areas 19–23 slice 12, and the
    // reason is worth keeping: they provably returned the SAME row as each other, and that row
    // could not answer the question either of them was named for.
    //
    // Slice 3 made this log effective-dated per SERIES — a reparent closes the open parent row, a
    // change of head closes the open head row, and the two move independently. The newest row in a
    // series is therefore always the open one, so filtering on `EffectiveTo == null` (`active`)
    // never changed the top of the same ordering `latest` used. Worse, both returned ONE row where
    // up to two arrangements are in force, so "the current arrangement" arrived as whichever series
    // happened to change last — a caller asking who heads a unit that was reparented afterwards got
    // a row with both head ids null.
    //
    // Neither had a caller. The unit's change-log tab reads `unit/{unitId}` — the whole log, which
    // states both series honestly — and that is the right home for the question. Same call as slice
    // 10 deleting `stats/by-department`: the wrong dimension, not a broken implementation.
}
