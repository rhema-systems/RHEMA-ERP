using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Api.Controllers.HR
{
    /// <summary>
    /// Admin-triggered leave year-end / cut-off processing: carry-over rollover, carry-over
    /// expiry, and forfeiture of unused accrual ("force leave").
    /// </summary>
    [ApiController]
    [Route("api/hr/leave-year-end")]
    [Authorize(Policy = "InternalOnly")]
    public class LeaveYearEndController : ControllerBase
    {
        private readonly ILeaveYearEndService _yearEndService;
        private readonly ILogger<LeaveYearEndController> _logger;

        public LeaveYearEndController(
            ILeaveYearEndService yearEndService,
            ILogger<LeaveYearEndController> logger)
        {
            _yearEndService = yearEndService;
            _logger = logger;
        }

        /// <summary>
        /// Rolls remaining available days from <paramref name="fromYear"/> into the next year's
        /// carried-over balance (capped at each leave type's MaxCarryOverDays). Idempotent.
        /// </summary>
        [HttpPost("carry-over")]
        // W3: a year-end job rewrites every balance in the tenant - admin tier only.
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveYearEndResult), StatusCodes.Status200OK)]
        public async Task<ActionResult<LeaveYearEndResult>> ProcessCarryOver(
            [FromQuery] int fromYear, [FromQuery] Guid? employeeId = null)
        {
            if (fromYear < 2000)
                return BadRequest(new { message = "A valid fromYear is required." });

            var result = await _yearEndService.ProcessCarryOverAsync(fromYear, employeeId);
            return Ok(result);
        }

        /// <summary>
        /// Forfeits unused accrual past the leave type's cut-off and expires carried-over days
        /// past their window. Idempotent per balance.
        /// </summary>
        [HttpPost("forfeiture")]
        // W3: a year-end job rewrites every balance in the tenant - admin tier only.
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveYearEndResult), StatusCodes.Status200OK)]
        public async Task<ActionResult<LeaveYearEndResult>> ProcessForfeiture(
            [FromQuery] int year, [FromQuery] DateOnly? asOf = null, [FromQuery] Guid? employeeId = null)
        {
            if (year < 2000)
                return BadRequest(new { message = "A valid year is required." });

            var result = await _yearEndService.ProcessForfeitureAsync(year, asOf, employeeId);
            return Ok(result);
        }
    }
}
