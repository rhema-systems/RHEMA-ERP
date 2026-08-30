using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
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
        private readonly IGeneralLedgerService _generalLedgerService;
        private readonly IFinanceSettingsService _financeSettingsService;

        public FiscalPeriodController(
            IFiscalPeriodService fiscalPeriodService,
            IGeneralLedgerService generalLedgerService,
            IFinanceSettingsService financeSettingsService)
        {
            _fiscalPeriodService = fiscalPeriodService;
            _generalLedgerService = generalLedgerService;
            _financeSettingsService = financeSettingsService;
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
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
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
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
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

        /// <summary>
        /// Updates safe fiscal-year metadata (name/notes).
        /// </summary>
        /// <remarks>
        /// Dates, period structure, and close state are intentionally excluded; those change
        /// through dedicated create/close/reopen operations.
        ///
        /// **Authorization:** Requires Finance administration permission.
        /// </remarks>
        [HttpPut("fiscal-years/{id}")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FiscalYearDto>> UpdateFiscalYear(Guid id, [FromBody] UpdateFiscalYearDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var year = await _fiscalPeriodService.UpdateFiscalYearAsync(id, dto);
                return Ok(year);
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Internal server error: {ex.Message}"); }
        }

        /// <summary>
        /// Performs the fiscal year-end close: zeroes revenue/expense accounts into retained
        /// earnings and marks the year closed.
        /// </summary>
        /// <remarks>
        /// **Business Rules:**
        /// - All fiscal periods in the year must already be closed.
        /// - The closing journal posts through the finance posting engine (posting event,
        ///   idempotent re-run, account balance snapshots).
        /// - The retained earnings account defaults from Finance Settings when not supplied.
        ///
        /// **Authorization:** Requires the close-accounting-periods permission.
        /// </remarks>
        /// <response code="200">Fiscal year closed; returns the close result.</response>
        /// <response code="400">Open periods remain, or no retained earnings account is configured.</response>
        /// <response code="404">Fiscal year not found.</response>
        [HttpPost("fiscal-years/{id}/close")]
        [Authorize(Policy = FinancePermissions.CloseAccountingPeriods)]
        public async Task<ActionResult<PeriodCloseResultDto>> CloseFiscalYear(Guid id, [FromBody] CloseFiscalYearRequestDto? dto = null)
        {
            try
            {
                var retainedEarningsAccountId = dto?.RetainedEarningsAccountId;
                if (retainedEarningsAccountId == null || retainedEarningsAccountId == Guid.Empty)
                {
                    var settings = await _financeSettingsService.GetSettingsAsync();
                    retainedEarningsAccountId = settings.RetainedEarningsAccountId;
                }

                if (retainedEarningsAccountId == null || retainedEarningsAccountId == Guid.Empty)
                {
                    return BadRequest(new PeriodCloseResultDto
                    {
                        Success = false,
                        Message = "No retained earnings account was supplied and none is configured in Finance Settings.",
                        Errors = new List<string> { "Configure a retained earnings account in Finance Settings or pass retainedEarningsAccountId." }
                    });
                }

                var result = await _generalLedgerService.CloseFiscalYearAsync(new YearEndCloseRequestDto
                {
                    FiscalYearId = id,
                    RetainedEarningsAccountId = retainedEarningsAccountId.Value,
                    ClosingNotes = dto?.ClosingNotes
                });

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Internal server error: {ex.Message}"); }
        }

        /// <summary>
        /// Reopens a closed fiscal year by reversing its year-end closing entry.
        /// </summary>
        /// <remarks>
        /// **Business Rules:**
        /// - A reason is mandatory and is appended to the year's closing notes for audit.
        /// - The closing journal is reversed through the finance posting engine.
        ///
        /// **Authorization:** Requires the reopen-accounting-periods permission.
        /// </remarks>
        [HttpPost("fiscal-years/{id}/reopen")]
        [Authorize(Policy = FinancePermissions.ReopenAccountingPeriods)]
        public async Task<ActionResult<PeriodCloseResultDto>> ReopenFiscalYear(Guid id, [FromBody] FiscalYearReopenRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _generalLedgerService.ReopenFiscalYearAsync(id, dto.Reason);
                return result.Success ? Ok(result) : BadRequest(result);
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
        /// - Multiple adjacent periods may be open while Finance completes the prior close
        /// - Future periods must be opened chronologically; gaps are not permitted
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
        /// Evaluates the live Finance close controls and appends immutable check evidence to the
        /// active numbered close cycle.
        /// </summary>
        [HttpPost("periods/{id}/close-workspace/evaluate")]
        [Authorize(Policy = FinancePermissions.MaintainCloseWorkspace)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> EvaluatePeriodCloseWorkspace(Guid id)
        {
            try
            {
                return Ok(await _fiscalPeriodService.EvaluatePeriodCloseWorkspaceAsync(id));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lists active, draft and superseded close-template versions for the current tenant.
        /// Historical versions remain visible because completed cycles retain their template ID.
        /// </summary>
        [HttpGet("close-templates")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<IReadOnlyList<FinanceCloseTemplateDto>>> GetFinanceCloseTemplates()
            => Ok(await _fiscalPeriodService.GetFinanceCloseTemplatesAsync());

        /// <summary>
        /// Creates the next draft version for a template code. Approval is deliberately separate
        /// so the author cannot activate their own control design.
        /// </summary>
        [HttpPost("close-templates/versions")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FinanceCloseTemplateDto>> CreateFinanceCloseTemplateVersion(
            [FromBody] SaveFinanceCloseTemplateVersionDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close template request is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.CreateFinanceCloseTemplateVersionAsync(dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Updates a draft only. Approved and superseded versions are immutable evidence.
        /// </summary>
        [HttpPut("close-templates/{id}")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FinanceCloseTemplateDto>> UpdateFinanceCloseTemplateDraft(
            Guid id,
            [FromBody] SaveFinanceCloseTemplateVersionDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close template request is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.UpdateFinanceCloseTemplateDraftAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Activates a reviewed draft and supersedes the prior active template for the same type.
        /// </summary>
        [HttpPost("close-templates/{id}/approve")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FinanceCloseTemplateDto>> ApproveFinanceCloseTemplate(
            Guid id,
            [FromBody] ApproveFinanceCloseTemplateDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close template approval is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.ApproveFinanceCloseTemplateAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Signs the maker declaration after the persisted mandatory checks pass. A different
        /// authorised user must subsequently review the evidence and close the period.
        /// </summary>
        [HttpPost("periods/{id}/close-workspace/prepare")]
        [Authorize(Policy = FinancePermissions.MaintainCloseWorkspace)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> PreparePeriodClose(
            Guid id,
            [FromBody] PeriodClosePreparationRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period close preparation request is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.PreparePeriodCloseAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Assigns, schedules or completes a manual task in the active close cycle. Automated
        /// tasks and maker-checker certification continue through their dedicated operations.
        /// </summary>
        [HttpPut("periods/{periodId}/close-workspace/tasks/{taskId}")]
        [Authorize(Policy = FinancePermissions.MaintainCloseWorkspace)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> UpdateFinanceCloseTask(
            Guid periodId,
            Guid taskId,
            [FromBody] UpdateFinanceCloseTaskDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close task update is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.UpdateFinanceCloseTaskAsync(periodId, taskId, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Links a file already uploaded through the shared controlled-upload service to one task
        /// in the active close cycle. Linking is separate from upload so failed network retries do
        /// not create duplicate business evidence rows.
        /// </summary>
        [HttpPost("periods/{periodId}/close-workspace/tasks/{taskId}/evidence")]
        [Authorize(Policy = FinancePermissions.MaintainCloseWorkspace)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> LinkFinanceCloseEvidence(
            Guid periodId,
            Guid taskId,
            [FromBody] LinkFinanceCloseEvidenceDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close evidence link request is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.LinkFinanceCloseEvidenceAsync(periodId, taskId, dto));
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Removes an unused evidence association while retaining the shared uploaded object. Any
        /// evidence already cited by a waiver or completed manual task is immutable.
        /// </summary>
        [HttpDelete("periods/{periodId}/close-workspace/tasks/{taskId}/evidence/{attachmentId}")]
        [Authorize(Policy = FinancePermissions.MaintainCloseWorkspace)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> RemoveFinanceCloseEvidence(
            Guid periodId,
            Guid taskId,
            Guid attachmentId)
        {
            try
            {
                return Ok(await _fiscalPeriodService.RemoveFinanceCloseEvidenceAsync(periodId, taskId, attachmentId));
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Requests acceptance of one eligible failed/warning check using evidence attached to the
        /// same task. Fundamental ledger controls are rejected by the domain service.
        /// </summary>
        [HttpPost("periods/{periodId}/close-workspace/checks/{snapshotId}/waivers")]
        [Authorize(Policy = FinancePermissions.RequestCloseExceptionWaivers)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> RequestFinanceCloseExceptionWaiver(
            Guid periodId,
            Guid snapshotId,
            [FromBody] RequestFinanceCloseWaiverDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close waiver request is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.RequestFinanceCloseExceptionWaiverAsync(periodId, snapshotId, dto));
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Records an independent waiver decision. Approval triggers an immediate full evaluation;
        /// it produces Waived only when the current exception fingerprint still matches.
        /// </summary>
        [HttpPost("periods/{periodId}/close-workspace/waivers/{waiverId}/review")]
        [Authorize(Policy = FinancePermissions.ApproveCloseExceptionWaivers)]
        public async Task<ActionResult<FinanceCloseWorkspaceDto>> ReviewFinanceCloseExceptionWaiver(
            Guid periodId,
            Guid waiverId,
            [FromBody] ReviewFinanceCloseWaiverDto dto)
        {
            if (dto == null)
                return BadRequest("Finance close waiver review is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.ReviewFinanceCloseExceptionWaiverAsync(periodId, waiverId, dto));
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Opens a Future fiscal period for transaction posting.
        /// </summary>
        /// <remarks>
        /// Opening is not reopening: a Future period has no signed close certificate to protect,
        /// while a Closed period must use the independent reopen request/review workflow.
        /// Earlier periods do not have to be closed, but they must already have left Future status
        /// so Finance cannot create a chronological gap in the accounting calendar.
        /// </remarks>
        [HttpPost("periods/{id}/open")]
        [Authorize(Policy = FinancePermissions.OpenAccountingPeriods)]
        public async Task<ActionResult<FiscalPeriodDto>> OpenPeriod(Guid id, [FromBody] PeriodOpenRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period open request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.FiscalPeriodId = id;
                return Ok(await _fiscalPeriodService.OpenPeriodAsync(dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Configures whether an open or future period accepts posting dates after today.
        /// This audited Finance-administrator control does not alter the period lifecycle.
        /// </summary>
        [HttpPut("periods/{id}/posting-date-policy")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FiscalPeriodDto>> UpdatePostingDatePolicy(
            Guid id,
            [FromBody] PeriodPostingDatePolicyRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Posting-date policy request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.UpdatePostingDatePolicyAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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
        [Authorize(Policy = FinancePermissions.CloseAccountingPeriods)]
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
        /// Submits a controlled request to reopen a closed fiscal period.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for period adjustments. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Period must be Closed (not Locked) to be reopened
        /// - Cannot reopen locked periods
        /// - The requester cannot approve the same request
        /// - Later closed/locked periods block the request so periods are reopened backwards
        /// - The books remain closed until a higher-tier approval is recorded
        ///
        /// **Effects:**
        /// - No posting state changes at request time
        /// - The affected-period snapshot is retained for stale-decision protection
        ///
        /// **Authorization:** Requires Finance.PeriodReopen permission
        /// </remarks>
        /// <param name="id">Fiscal period ID requested for reopen</param>
        /// <returns>Persisted request awaiting independent review</returns>
        /// <response code="200">Reopen request submitted successfully</response>
        /// <response code="400">Cannot reopen - period is locked</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/reopen-requests")]
        [Authorize(Policy = FinancePermissions.ReopenAccountingPeriods)]
        public async Task<ActionResult<FinancePeriodReopenRequestDto>> RequestPeriodReopen(
            Guid id,
            [FromBody] PeriodReopenRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period reopen request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.FiscalPeriodId = id;
                var result = await _fiscalPeriodService.RequestPeriodReopenAsync(dto);
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
        /// Records a higher-tier decision on a pending accounting-period reopen request.
        /// </summary>
        /// <remarks>
        /// Approval re-runs the immutable affected-period validation, supersedes the old signed
        /// certificate, reopens the books, and starts cycle N+1 in one serialised transaction.
        /// Rejection retains the request and reviewer declaration without changing posting state.
        ///
        /// **Authorization:** Requires Finance.PeriodReopen.Approve permission.
        /// </remarks>
        [HttpPost("periods/{periodId}/reopen-requests/{requestId}/review")]
        [Authorize(Policy = FinancePermissions.ApproveAccountingPeriodReopens)]
        public async Task<ActionResult<FinancePeriodReopenRequestDto>> ReviewPeriodReopen(
            Guid periodId,
            Guid requestId,
            [FromBody] PeriodReopenReviewDto dto)
        {
            if (dto == null)
                return BadRequest("Period reopen review is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                return Ok(await _fiscalPeriodService.ReviewPeriodReopenAsync(periodId, requestId, dto));
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
        /// Applies the global lock to an already closed fiscal period.
        /// </summary>
        /// <remarks>
        /// The service has always treated this as the highest protection level. Exposing the
        /// matching endpoint keeps the HTTP contract aligned with the service and frontend client.
        /// </remarks>
        [HttpPost("periods/{id}/lock")]
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
        public async Task<ActionResult<FiscalPeriodDto>> LockPeriod(Guid id, [FromBody] PeriodLockRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Period lock request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.FiscalPeriodId = id;
                return Ok(await _fiscalPeriodService.LockPeriodAsync(dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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
        [Authorize(Policy = FinancePermissions.AdministerFinance)]
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
        /// Retrieves the tenant's enabled top-level modules that post to Finance.
        /// </summary>
        /// <remarks>
        /// Finance is always returned. Other modules appear only when enabled for the tenant
        /// and included in the Finance module-lock catalog.
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
        /// - Module must be enabled for the tenant and integrated with Finance
        /// - Period must be open and not globally locked
        /// - Audit trail is logged
        ///
        /// **Authorization:** Requires Finance.PeriodClose permission
        /// </remarks>
        /// <param name="id">Fiscal period ID</param>
        /// <param name="dto">Module lock request with module code and reason</param>
        /// <returns>Success message with lock ID</returns>
        /// <response code="200">Module locked successfully</response>
        /// <response code="400">Invalid module code or business rule violation</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/lock-module")]
        [Authorize(Policy = FinancePermissions.CloseAccountingPeriods)]
        public async Task<ActionResult> LockPeriodForModule(Guid id, [FromBody] ModuleLockRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    return BadRequest("A reason is required to lock a module.");

                var result = await _fiscalPeriodService.LockPeriodForModuleAsync(id, dto.ModuleCode, dto.Reason);
                return Ok(new { message = $"Module {dto.ModuleCode} locked successfully", period = result });
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>
        /// Temporarily reopens a specific module for the given fiscal period.
        /// </summary>
        /// <remarks>
        /// Allows final Finance postings originating from one module until the required expiry.
        /// Drafting, editing, viewing, and approval remain available while a module is locked.
        ///
        /// **Business Rules:**
        /// - Module must be enabled for the tenant and integrated with Finance
        /// - A reason and an expiry no more than 24 hours away are required
        /// - Reopening inside a global lock converts the period to a partial lock
        /// - Audit trail is logged
        ///
        /// **Authorization:** Requires Finance.PeriodReopen permission
        /// </remarks>
        /// <param name="id">Fiscal period ID</param>
        /// <param name="dto">Module reopening request with module code, reason, and expiry</param>
        /// <returns>Success message</returns>
        /// <response code="200">Module temporarily reopened successfully</response>
        /// <response code="400">Module is not locked or a business rule was violated</response>
        /// <response code="404">Fiscal period not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/unlock-module")]
        [Authorize(Policy = FinancePermissions.ReopenAccountingPeriods)]
        public async Task<ActionResult> UnlockPeriodForModule(Guid id, [FromBody] ModuleLockRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    return BadRequest("A reason is required to reopen a module.");
                if (!dto.ReopenUntilUtc.HasValue)
                    return BadRequest("An automatic reopening expiry is required.");

                var result = await _fiscalPeriodService.UnlockPeriodForModuleAsync(
                    id,
                    dto.ModuleCode,
                    dto.Reason,
                    dto.ReopenUntilUtc.Value);
                return Ok(new
                {
                    message = $"Module {dto.ModuleCode} reopened until {dto.ReopenUntilUtc.Value:u}",
                    period = result
                });
            }
            catch (ArgumentException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        #endregion

    }
}
