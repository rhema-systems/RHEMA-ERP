using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Holiday calendar management and calendar-scoped public holiday operations.
/// </summary>
[ApiController]
[Route("api/holiday-calendars")]
[Authorize(Policy = "InternalOnly")]
public class HolidayCalendarsController : AttendanceControllerBase
{
    private readonly IHolidayCalendarService _calendarService;
    private readonly IPublicHolidayService _holidayService;

    public HolidayCalendarsController(
        IHolidayCalendarService calendarService,
        IPublicHolidayService holidayService,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _calendarService = calendarService;
        _holidayService = holidayService;
    }

    // =========================================================================
    // CALENDAR QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HolidayCalendarSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _calendarService.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<HolidayCalendarSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _calendarService.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("default")]
    public async Task<ActionResult<HolidayCalendarDto?>> GetDefaultCalendar(CancellationToken ct = default)
        => Ok(await _calendarService.GetDefaultCalendarAsync(ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<HolidayCalendarSummaryDto>>> GetActiveCalendars(
        CancellationToken ct = default)
        => Ok(await _calendarService.GetActiveCalendarsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HolidayCalendarDto>> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _calendarService.GetByIdAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/with-holidays")]
    public async Task<ActionResult<HolidayCalendarDto>> GetWithHolidays(
        Guid id,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _calendarService.GetWithHolidaysAsync(id, year, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // CALENDAR CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<HolidayCalendarDto>> Create(
        [FromBody] CreateHolidayCalendarDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _calendarService.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HolidayCalendarDto>> Update(
        Guid id,
        [FromBody] UpdateHolidayCalendarDto dto,
        CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        try
        {
            return Ok(await _calendarService.UpdateAsync(dto, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _calendarService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // CALENDAR-SCOPED PUBLIC HOLIDAY OPERATIONS
    // =========================================================================

    [HttpGet("{id:guid}/holidays")]
    public async Task<ActionResult<IEnumerable<PublicHolidaySummaryDto>>> GetHolidays(
        Guid id,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await _calendarService.GetHolidaysAsync(id, year, ct));

    [HttpGet("{id:guid}/holidays/recurring")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> GetRecurringHolidays(
        Guid id,
        CancellationToken ct = default)
        => Ok(await _holidayService.GetRecurringHolidaysAsync(id, ct));

    [HttpGet("{id:guid}/holidays/{holidayId:guid}")]
    public async Task<ActionResult<PublicHolidayDto>> GetHolidayById(
        Guid id,
        Guid holidayId,
        CancellationToken ct = default)
    {
        try
        {
            var holiday = await _calendarService.GetHolidayByIdAsync(holidayId, ct);
            if (holiday.HolidayCalendarId != id)
                return NotFound(new { message = $"Public holiday '{holidayId}' does not belong to calendar '{id}'." });

            return Ok(holiday);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/holidays")]
    public async Task<ActionResult<PublicHolidayDto>> AddHoliday(
        Guid id,
        [FromBody] CreatePublicHolidayDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.DateTo < dto.DateFrom)
            return BadRequest(new { message = "DateTo must be on or after DateFrom." });
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.HolidayCalendarId = id;

        try
        {
            var created = await _calendarService.AddHolidayAsync(dto, tenantId, employeeId, ct);
            return CreatedAtAction(nameof(GetHolidayById), new { id, holidayId = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/holidays/{holidayId:guid}")]
    public async Task<ActionResult<PublicHolidayDto>> UpdateHoliday(
        Guid id,
        Guid holidayId,
        [FromBody] UpdatePublicHolidayDto dto,
        CancellationToken ct = default)
    {
        if (holidayId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.DateTo < dto.DateFrom)
            return BadRequest(new { message = "DateTo must be on or after DateFrom." });
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        try
        {
            var existing = await _calendarService.GetHolidayByIdAsync(holidayId, ct);
            if (existing.HolidayCalendarId != id)
                return NotFound(new { message = $"Public holiday '{holidayId}' does not belong to calendar '{id}'." });

            return Ok(await _calendarService.UpdateHolidayAsync(dto, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/holidays/{holidayId:guid}")]
    public async Task<IActionResult> DeleteHoliday(
        Guid id,
        Guid holidayId,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await _calendarService.GetHolidayByIdAsync(holidayId, ct);
            if (existing.HolidayCalendarId != id)
                return NotFound(new { message = $"Public holiday '{holidayId}' does not belong to calendar '{id}'." });

            await _calendarService.DeleteHolidayAsync(holidayId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // GLOBAL HOLIDAY LOOKUP (Leave module reads + calendar management helpers)
    // =========================================================================

    /// <summary>Tenant-wide holidays for a year — used by Leave calendar and leave calculations.</summary>
    [HttpGet("holidays/by-year/{year:int}")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> GetHolidaysByYear(
        int year,
        CancellationToken ct = default)
        => Ok(await _holidayService.GetByYearAsync(year, ct));

    [HttpGet("holidays/range")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> GetHolidaysInRange(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
    {
        if (to < from)
            return BadRequest(new { message = "Date 'to' must be on or after 'from'." });

        return Ok(await _holidayService.GetInRangeAsync(from, to, ct));
    }

    [HttpGet("holidays/{holidayId:guid}")]
    public async Task<ActionResult<PublicHolidayDto>> GetHolidayByIdGlobal(
        Guid holidayId,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _calendarService.GetHolidayByIdAsync(holidayId, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
