using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Aggregated dashboard for the Attendance &amp; Time module.
///
/// One endpoint rather than a dozen: the screen previously had to call six list endpoints and
/// count the results client-side, which shipped whole collections just to take their length
/// and could not produce the trend or the risk ranking at all.
/// </summary>
[ApiController]
[Route("api/attendance-dashboard")]
[Authorize]
public class AttendanceDashboardController : AttendanceControllerBase
{
    private readonly IAttendanceDashboardService _service;

    public AttendanceDashboardController(
        IAttendanceDashboardService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    /// <summary>
    /// Today's snapshot, the open pay period, approval backlogs, alert counts, a short
    /// attendance trend and the chronic-absentee list.
    /// </summary>
    /// <param name="asOf">Day treated as "today". Defaults to the current UTC date.</param>
    /// <param name="trendDays">Length of the daily trend, clamped to 1–90.</param>
    /// <param name="riskListSize">Size of the chronic-absentee list, clamped to 1–50.</param>
    [HttpGet]
    public async Task<ActionResult<AttendanceDashboardDto>> Get(
        [FromQuery] DateOnly? asOf = null,
        [FromQuery] int trendDays = 7,
        [FromQuery] int riskListSize = 5,
        CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(asOf, trendDays, riskListSize, ct));
}
