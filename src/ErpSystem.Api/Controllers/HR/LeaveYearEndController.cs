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
        /// Refused with 400 for a year that has not ended, except as a preview (round 5, lane G).
        /// </summary>
        [HttpPost("carry-over")]
        // W3: a year-end job rewrites every balance in the tenant - admin tier only.
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveYearEndResult), StatusCodes.Status200OK)]
        /// <param name="dryRun">
        /// ⚠ Compute and report, write nothing. This job moves people's balances in bulk and has no
        /// undo, so a preview is the difference between catching a misconfigured leave type before
        /// the run and catching it in nine hundred balances afterwards (finding L-24).
        /// </param>
        public async Task<ActionResult<LeaveYearEndResult>> ProcessCarryOver(
            [FromQuery] int fromYear, [FromQuery] Guid? employeeId = null, [FromQuery] bool dryRun = false)
        {
            if (fromYear < 2000)
                return BadRequest(new { message = "A valid fromYear is required." });

            try
            {
                var result = await _yearEndService.ProcessCarryOverAsync(fromYear, employeeId, dryRun);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // A year that has not ended cannot be carried from (round 5, lane G) — say which day
                // it can, rather than 500.
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Forfeits unused accrual past the leave type's cut-off and expires the carried-over days
        /// not taken before their window closed — only those (round 5, lane G). Idempotent per balance.
        /// </summary>
        [HttpPost("forfeiture")]
        // W3: a year-end job rewrites every balance in the tenant - admin tier only.
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveYearEndResult), StatusCodes.Status200OK)]
        /// <param name="dryRun">⚠ Compute and report, write nothing (finding L-24).</param>
        public async Task<ActionResult<LeaveYearEndResult>> ProcessForfeiture(
            [FromQuery] int year, [FromQuery] DateOnly? asOf = null, [FromQuery] Guid? employeeId = null,
            [FromQuery] bool dryRun = false)
        {
            if (year < 2000)
                return BadRequest(new { message = "A valid year is required." });

            try
            {
                // ⚠ A dry run still requires a linked actor. The preview must fail wherever the real
                // run would, or it is not a preview of anything.
                var result = await _yearEndService.ProcessForfeitureAsync(year, asOf, employeeId, dryRun);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // An admin account not linked to an employee cannot be the forfeiture's actor
                // (PerformedBy is an Employee foreign key) — say so rather than 500.
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
