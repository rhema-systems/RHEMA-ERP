using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages fiscal years and periods for financial transaction control.
    /// </summary>
    /// <remarks>
    /// This controller provides fiscal period management which is critical for all ERP modules.
    /// All modules must validate transaction dates against open fiscal periods before posting
    /// any financial transactions.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance")]
    public class FiscalPeriodController : ControllerBase
    {
        private readonly IFiscalPeriodService _fiscalPeriodService;

        public FiscalPeriodController(IFiscalPeriodService fiscalPeriodService)
        {
            _fiscalPeriodService = fiscalPeriodService;
        }

        #region Fiscal Years

        /// <summary>
        /// Retrieves all fiscal years for the current tenant.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Display fiscal year dropdown in transaction entry
        /// - Period selection for reporting
        /// - Fiscal year setup and management
        ///
        /// **Integration Pattern:**
        /// - Call on module initialization to cache fiscal years
        /// - Use to determine current active fiscal year
        ///
        /// **Business Rules:**
        /// - Returns all fiscal years (open, closed, and locked)
        /// - Ordered by year descending (newest first)
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <returns>List of fiscal years</returns>
        /// <response code="200">Returns the list of fiscal years</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("fiscal-years")]
        public async Task<ActionResult<List<FiscalYearDto>>> GetFiscalYears()
        {
            try
            {
                var years = await _fiscalPeriodService.GetFiscalYearsAsync();
                return Ok(years);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific fiscal year by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Get fiscal year details
        /// - Validate fiscal year exists
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="id">Fiscal year ID</param>
        /// <returns>Fiscal year details</returns>
        /// <response code="200">Returns the fiscal year</response>
        /// <response code="404">Fiscal year not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("fiscal-years/{id}")]
        public async Task<ActionResult<FiscalYearDto>> GetFiscalYearById(Guid id)
        {
            try
            {
                var year = await _fiscalPeriodService.GetFiscalYearByIdAsync(id);
                if (year == null)
                    return NotFound($"Fiscal year with ID {id} not found");

                return Ok(year);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new fiscal year with periods.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module during setup. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Automatically creates fiscal periods based on numberOfPeriods
        /// - Cannot create overlapping fiscal years
        /// - Start date must be before end date
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="dto">Fiscal year creation details</param>
        /// <returns>Created fiscal year with generated periods</returns>
        /// <response code="201">Fiscal year created successfully</response>
        /// <response code="400">Invalid data or overlapping dates</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("fiscal-years")]
        public async Task<ActionResult<FiscalYearDto>> CreateFiscalYear([FromBody] CreateFiscalYearDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var year = await _fiscalPeriodService.CreateFiscalYearAsync(dto);
                return CreatedAtAction(nameof(GetFiscalYearById), new { id = year.Id }, year);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a fiscal year.
        /// </summary>
        /// <remarks>
        /// **Business Rules:**
        /// - Cannot delete closed fiscal years
        /// - Cannot delete fiscal years with transactions
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="id">Fiscal year ID to delete</param>
        /// <returns>Success message</returns>
        /// <response code="200">Fiscal year deleted successfully</response>
        /// <response code="400">Cannot delete due to business rules (e.g. transactions exist)</response>
        /// <response code="404">Fiscal year not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("fiscal-years/{id}")]
        public async Task<IActionResult> DeleteFiscalYear(Guid id)
        {
            try
            {
                await _fiscalPeriodService.DeleteFiscalYearAsync(id);
                return Ok(new { message = "Fiscal year deleted successfully" });
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Internal server error: {ex.Message}"); }
        }

        #endregion

        #region Fiscal Periods

        /// <summary>
        /// Retrieves fiscal periods with optional filtering by fiscal year.
        /// </summary>
        /// <remarks>
        /// **CRITICAL ENDPOINT:** All modules MUST validate transaction dates against open periods.
        ///
        /// **Common Use Cases:**
        /// - **Transaction Date Validation:** Check if transaction date falls in an open period
        /// - **Period Selection:** Display available periods for transaction entry
        /// - **Period-End Processing:** Identify periods ready for closing
        ///
        /// **Integration Pattern for ALL Modules:**
        /// ```
        /// Before posting any transaction:
        /// 1. GET /api/Finance/fiscal-periods
        /// 2. Find period where transaction date falls between startDate and endDate
        /// 3. Check if period status is "Open"
        /// 4. If period is closed or locked, reject transaction
        /// 5. Use period ID in journal entry creation
        /// ```
        ///
        /// **Module Integration Examples:**
        ///
        /// **Sales Module - Invoice Posting:**
        /// - Validate invoice date against open periods
        /// - Prevent posting invoices to closed periods
        ///
        /// **Inventory Module - Stock Movement:**
        /// - Validate movement date against open periods
        /// - Ensure COGS posting is in open period
        ///
        /// **Payroll Module - Payroll Run:**
        /// - Validate payroll date against open periods
        /// - Prevent posting to closed months
        ///
        /// **Business Rules:**
        /// - Periods have three statuses: Open, Closed, Locked
        /// - Open: Transactions can be posted
        /// - Closed: No new transactions, can be reopened
        /// - Locked: Permanently closed, cannot be reopened
        /// - Only one period should be open at a time (best practice)
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="yearId">Optional fiscal year ID to filter periods</param>
        /// <returns>List of fiscal periods</returns>
        /// <response code="200">Returns the list of fiscal periods</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("fiscal-periods")]
        public async Task<ActionResult<List<FiscalPeriodDto>>> GetFiscalPeriods(
            [FromQuery] Guid? yearId = null,
            [FromQuery] Guid? fiscalYearId = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var periods = await _fiscalPeriodService.GetFiscalPeriodsAsync(yearId ?? fiscalYearId, status);
                return Ok(periods);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific fiscal period by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Get period details
        /// - Check period status before posting
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="id">Fiscal period ID</param>
        /// <returns>Fiscal period details</returns>
        /// <response code="200">Returns the fiscal period</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("fiscal-periods/{id}")]
        public async Task<ActionResult<FiscalPeriodDto>> GetFiscalPeriodById(Guid id)
        {
            try
            {
                var period = await _fiscalPeriodService.GetFiscalPeriodByIdAsync(id);
                if (period == null)
                    return NotFound($"Fiscal period with ID {id} not found");

                return Ok(period);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes a fiscal period, preventing new transactions from being posted.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module during period-end close. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Period must be Open to be closed
        /// - Cannot close if there are unposted draft entries
        /// - Cannot close if period is not balanced (optional validation)
        /// - Closed periods can be reopened if not locked
        ///
        /// **Effects:**
        /// - Period status changes to "Closed"
        /// - No new transactions can be posted to this period
        /// - Other modules will receive error when trying to post to this period
        ///
        /// **Authorization:** Requires Finance.PeriodClose permission
        /// </remarks>
        /// <param name="id">Fiscal period ID to close</param>
        /// <param name="dto">Period close request with optional validation settings</param>
        /// <returns>Success message</returns>
        /// <response code="200">Period closed successfully</response>
        /// <response code="400">Cannot close - period has unposted entries or other violations</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/close")]
        public async Task<ActionResult> ClosePeriod(Guid id, [FromBody] PeriodCloseRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period close request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.FiscalPeriodId = id;
                var result = await _fiscalPeriodService.ClosePeriodAsync(dto);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reopens a closed fiscal period to allow posting corrections.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for period adjustments. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Period must be Closed (not Locked) to be reopened
        /// - Cannot reopen locked periods
        /// - Requires approval/authorization
        ///
        /// **Effects:**
        /// - Period status changes to "Open"
        /// - Transactions can be posted again
        /// - Other modules can post to this period
        ///
        /// **Authorization:** Requires Finance.PeriodReopen permission
        /// </remarks>
        /// <param name="id">Fiscal period ID to reopen</param>
        /// <returns>Success message</returns>
        /// <response code="200">Period reopened successfully</response>
        /// <response code="400">Cannot reopen - period is locked</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/reopen")]
        public async Task<ActionResult> ReopenPeriod(Guid id, [FromBody] PeriodReopenRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period reopen request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.FiscalPeriodId = id;
                var result = await _fiscalPeriodService.ReopenPeriodAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Unlocks a locked fiscal period (emergency use only).
        /// </summary>
        /// <remarks>
        /// Requires highest level authorization. Should rarely be used.
        ///
        /// **Business Rules:**
        /// - Requires Finance.Admin permission
        /// - Audit trail is logged
        /// - Reason must be provided
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="id">Fiscal period ID to unlock</param>
        /// <returns>Success message</returns>
        /// <response code="200">Period unlocked successfully</response>
        /// <response code="400">Cannot unlock</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/unlock")]
        public async Task<ActionResult> UnlockPeriod(Guid id, [FromBody] PeriodUnlockRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period unlock request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _fiscalPeriodService.UnlockPeriodAsync(id, dto.Reason);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion
        #region Module Locking

        /// <summary>
        /// Retrieves all defined system modules available for locking.
        /// </summary>
        /// <remarks>
        /// Returns a list of module definitions that can be locked for specific fiscal periods.
        /// This allows granular control over which modules can post to which periods.
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <returns>List of module definitions</returns>
        /// <response code="200">Returns the list of module definitions</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("modules")]
        public async Task<ActionResult<List<ModuleDefinitionDto>>> GetModules()
        {
            try
            {
                var modules = await _fiscalPeriodService.GetModuleDefinitionsAsync();
                return Ok(modules);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Locks a specific module for the given fiscal period.
        /// </summary>
        /// <remarks>
        /// Prevents a specific module from posting transactions to the given period
        /// while allowing other modules to continue posting.
        ///
        /// **Business Rules:**
        /// - Module must be a valid system module
        /// - Period must exist and not be fully locked
        /// - Audit trail is logged
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="id">Fiscal period ID</param>
        /// <param name="dto">Module lock request with module code and reason</param>
        /// <returns>Success message with lock ID</returns>
        /// <response code="200">Module locked successfully</response>
        /// <response code="400">Invalid module code or business rule violation</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/lock-module")]
        public async Task<ActionResult> LockPeriodForModule(Guid id, [FromBody] ModuleLockRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Module locked from finance administration." : dto.Reason;
                var result = await _fiscalPeriodService.LockPeriodForModuleAsync(id, dto.ModuleCode, reason);
                return Ok(new { message = $"Module {dto.ModuleCode} locked successfully", id = result });
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>
        /// Unlocks a specific module for the given fiscal period.
        /// </summary>
        /// <remarks>
        /// Allows a previously locked module to resume posting transactions to the given period.
        ///
        /// **Business Rules:**
        /// - Module must be currently locked for this period
        /// - Period must not be fully locked
        /// - Audit trail is logged
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="id">Fiscal period ID</param>
        /// <param name="dto">Module unlock request with module code and reason</param>
        /// <returns>Success message</returns>
        /// <response code="200">Module unlocked successfully</response>
        /// <response code="400">Module not locked or business rule violation</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/unlock-module")]
        public async Task<ActionResult> UnlockPeriodForModule(Guid id, [FromBody] ModuleLockRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Module unlocked from finance administration." : dto.Reason;
                var result = await _fiscalPeriodService.UnlockPeriodForModuleAsync(id, dto.ModuleCode, reason);
                return Ok(new { message = $"Module {dto.ModuleCode} unlocked successfully" });
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        #endregion

    }
}
