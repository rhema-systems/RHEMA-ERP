using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Controllers
{
    /// <summary>
    /// Core finance controller responsible for General Ledger operations, financial statement generation, currency revaluation, and fiscal period/year lifecycle management.
    /// </summary>
    /// <remarks>
    /// **Domain Responsibility:**
    /// This controller serves as the primary entry point for the finance module, orchestrating
    /// journal entry posting to the General Ledger, producing statutory financial reports
    /// (Balance Sheet, Income Statement, Trial Balance, Cash Flow Statement), running
    /// multi-currency revaluation processes, and managing the open/close/lock lifecycle
    /// of fiscal periods and fiscal years.
    ///
    /// **Key Integrations:**
    /// - General Ledger Service: Journal entry posting, financial statement generation, period management
    /// - Currency Revaluation Service: Foreign currency gain/loss adjustments
    /// - Chart of Accounts: All GL postings and reports reference the tenant's chart of accounts
    /// - Fiscal Calendar: Period and year close operations enforce the fiscal calendar hierarchy
    ///
    /// **Multi-Tenant Awareness:**
    /// All operations are scoped to the authenticated user's tenant via ICurrentUserService.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FinanceController : ControllerBase
    {
        private readonly IGeneralLedgerService _glService;
        private readonly ICurrencyRevaluationService _revaluationService;
        private readonly ICurrentUserService _currentUserService;

        public FinanceController(
            IGeneralLedgerService glService,
            ICurrencyRevaluationService revaluationService,
            ICurrentUserService currentUserService)
        {
            _glService = glService;
            _revaluationService = revaluationService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Returns the current user's authentication claims and tenant context for diagnostic purposes.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Troubleshooting authentication or authorization issues in non-production environments
        /// - Verifying that the tenant ID is correctly resolved from the JWT token
        /// - Confirming the authenticated identity and claim set during integration testing
        ///
        /// **Integration Pattern:**
        /// - Reads claims directly from the HttpContext User principal
        /// - Resolves tenant ID through ICurrentUserService to confirm multi-tenant isolation
        ///
        /// **Business Rules:**
        /// - This is a debug/diagnostic endpoint and should be disabled or secured in production
        /// - No data is modified; this is a read-only introspection endpoint
        /// </remarks>
        /// <returns>
        /// An object containing the user's claims list, resolved tenant ID, authentication status, and user name.
        /// </returns>
        /// <response code="200">Claims and tenant context returned successfully</response>
        /// <response code="401">Not authenticated - no valid bearer token provided</response>
        [HttpGet("debug/claims")]
        public IActionResult GetClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var tenantId = _glService.GetType().GetProperty("_currentUserService",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return Ok(new
            {
                claims = claims,
                tenantIdFromService = _currentUserService?.TenantId,
                isAuthenticated = User.Identity?.IsAuthenticated,
                userName = User.Identity?.Name
            });
        }

        /// <summary>
        /// Posts a new journal entry to the General Ledger with its constituent debit and credit lines.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Recording manual adjusting entries (accruals, deferrals, reclassifications)
        /// - Posting system-generated entries from sub-ledgers (AP, AR, Payroll, Inventory)
        /// - Recording opening balances during initial system setup or migration
        ///
        /// **Integration Pattern:**
        /// - Downstream from sub-ledger modules that generate GL postings automatically
        /// - Each journal entry updates account balances and is reflected in all financial statements
        /// - Entries are posted against the active fiscal period; closed or locked periods will be rejected
        ///
        /// **Business Rules:**
        /// - Total debits must equal total credits (the entry must balance)
        /// - The fiscal period for the entry date must be open and not locked
        /// - All referenced GL account codes must exist in the tenant's chart of accounts
        /// - Multi-currency entries must include exchange rate information
        ///
        /// **Authorization:** Requires authenticated user with GL posting permission
        /// </remarks>
        /// <param name="entryDto">The journal entry payload including header details (date, reference, narration) and line items (account, debit/credit amounts, currency).</param>
        /// <returns>The created journal entry with its system-assigned entry number and posted line details.</returns>
        /// <response code="200">Journal entry posted successfully</response>
        /// <response code="400">Validation failure - unbalanced entry, invalid account, closed period, or other business rule violation</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("journal-entries")]
        public async Task<IActionResult> PostJournalEntry([FromBody] CreateJournalEntryDto entryDto)
        {
            try
            {
                var journalEntry = await _glService.PostJournalEntryAsync(entryDto);
                return Ok(journalEntry);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Executes a foreign currency revaluation process, generating gain/loss journal entries for accounts denominated in foreign currencies.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Month-end or period-end revaluation of foreign-currency-denominated balances
        /// - Adjusting unrealised gains and losses on open AP/AR balances
        /// - Revaluing bank account balances held in foreign currencies
        ///
        /// **Integration Pattern:**
        /// - Reads current exchange rates and compares them against the rates at which transactions were originally posted
        /// - Produces an adjusting journal entry that is posted to the GL via the General Ledger Service
        /// - The resulting entry appears in the Trial Balance and Balance Sheet under the configured gain/loss accounts
        ///
        /// **Business Rules:**
        /// - Revaluation is only meaningful for accounts with a functional currency different from the reporting currency
        /// - Exchange rates for the revaluation date must be available in the system
        /// - The target fiscal period must be open
        /// - Revaluation can be run multiple times; each run reverses the prior unrealised adjustment
        ///
        /// **Authorization:** Requires authenticated user with currency revaluation permission
        /// </remarks>
        /// <param name="requestDto">The revaluation request specifying the revaluation date, target currency pairs, and account scope.</param>
        /// <returns>The generated revaluation journal entry with gain/loss line items.</returns>
        /// <response code="200">Revaluation completed and journal entry posted successfully</response>
        /// <response code="400">Validation failure - missing exchange rates, closed period, or invalid account scope</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("revaluation")]
        public async Task<IActionResult> RunRevaluation([FromBody] RevaluationRequestDto requestDto)
        {
            try
            {
                var journalEntry = await _revaluationService.RunCurrencyRevaluationAsync(requestDto);
                return Ok(journalEntry);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generates a Balance Sheet (Statement of Financial Position) as of the specified date.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Producing the statutory Balance Sheet for regulatory or board reporting
        /// - Reviewing asset, liability, and equity positions at any point in time
        /// - Comparing Balance Sheet positions across periods for trend analysis
        ///
        /// **Integration Pattern:**
        /// - Aggregates GL account balances classified as Assets, Liabilities, and Equity
        /// - Balances include all posted journal entries up to and including the report date
        /// - Works in conjunction with the Chart of Accounts account classification hierarchy
        ///
        /// **Business Rules:**
        /// - Assets must equal Liabilities plus Equity (the accounting equation must hold)
        /// - Only posted (not draft or voided) journal entries are included
        /// - Retained earnings are computed from cumulative Income Statement results of prior closed years
        /// - Multi-currency balances are converted to the reporting currency at the report-date exchange rate
        ///
        /// **Authorization:** Requires authenticated user with financial reporting permission
        /// </remarks>
        /// <param name="request">Query parameters including the as-of date, comparative period options, and optional department/cost-centre filters.</param>
        /// <returns>The Balance Sheet report with categorised account groups, subtotals, and grand totals.</returns>
        /// <response code="200">Balance Sheet generated successfully</response>
        /// <response code="400">Validation failure - invalid date range or filter parameters</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("statements/balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet([FromQuery] BalanceSheetRequestDto request)
        {
            try
            {
                var balanceSheet = await _glService.GenerateBalanceSheetAsync(request);
                return Ok(balanceSheet);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generates an Income Statement (Profit and Loss Statement) for the specified period.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Producing the statutory Income Statement for regulatory, tax, or board reporting
        /// - Analysing revenue and expense performance for a given fiscal period or date range
        /// - Comparing actual results against budget or prior-period figures
        ///
        /// **Integration Pattern:**
        /// - Aggregates GL account balances classified as Revenue and Expense for the requested period
        /// - Revenue and expense accounts are reset at the start of each fiscal year via year-end close
        /// - Net income/loss flows into Retained Earnings on the Balance Sheet
        ///
        /// **Business Rules:**
        /// - Only posted journal entries within the specified date range are included
        /// - Revenue accounts carry credit balances; expense accounts carry debit balances
        /// - The report calculates Gross Profit, Operating Income, and Net Income subtotals
        /// - Multi-currency transactions are converted at the transaction-date exchange rate
        ///
        /// **Authorization:** Requires authenticated user with financial reporting permission
        /// </remarks>
        /// <param name="request">Query parameters including the period start/end dates, comparative options, and optional department/cost-centre filters.</param>
        /// <returns>The Income Statement report with revenue and expense categories, subtotals, and net income.</returns>
        /// <response code="200">Income Statement generated successfully</response>
        /// <response code="400">Validation failure - invalid date range or filter parameters</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("statements/income-statement")]
        public async Task<IActionResult> GetIncomeStatement([FromQuery] IncomeStatementRequestDto request)
        {
            try
            {
                var incomeStatement = await _glService.GenerateIncomeStatementAsync(request);
                return Ok(incomeStatement);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generates a Trial Balance listing all GL accounts with their debit and credit balances.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Verifying that total debits equal total credits across the entire ledger
        /// - Reviewing account-level balances before producing financial statements
        /// - Providing auditors with a detailed listing of all GL account balances
        ///
        /// **Integration Pattern:**
        /// - Reads balances from every GL account in the Chart of Accounts
        /// - Serves as the foundational data set from which the Balance Sheet and Income Statement are derived
        /// - Can be filtered by date range to show balances as of a specific period
        ///
        /// **Business Rules:**
        /// - Total debits must equal total credits; any imbalance indicates a posting error
        /// - Only posted journal entries are included (draft and voided entries are excluded)
        /// - Accounts with zero balances may be included or excluded based on request parameters
        /// - The Trial Balance reflects the cumulative effect of all entries up to the specified date
        ///
        /// **Authorization:** Requires authenticated user with financial reporting permission
        /// </remarks>
        /// <param name="request">Query parameters including the as-of date, option to include zero-balance accounts, and optional account range filters.</param>
        /// <returns>The Trial Balance report with per-account debit/credit columns and totals.</returns>
        /// <response code="200">Trial Balance generated successfully</response>
        /// <response code="400">Validation failure - invalid date range or filter parameters</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("statements/trial-balance")]
        public async Task<IActionResult> GetTrialBalance([FromQuery] TrialBalanceRequestDto request)
        {
            try
            {
                var trialBalance = await _glService.GenerateTrialBalanceAsync(request);
                return Ok(trialBalance);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generates a Cash Flow Statement showing cash inflows and outflows categorised by operating, investing, and financing activities.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Producing the statutory Cash Flow Statement for regulatory or board reporting
        /// - Analysing liquidity and cash generation capacity over a fiscal period
        /// - Identifying major sources and uses of cash for management decision-making
        ///
        /// **Integration Pattern:**
        /// - Derives cash movements from changes in GL account balances between the start and end of the period
        /// - Uses the indirect method (adjusting net income for non-cash items) or direct method based on configuration
        /// - Reconciles to the change in cash and cash equivalents on the Balance Sheet
        ///
        /// **Business Rules:**
        /// - Cash and cash equivalents accounts must be correctly tagged in the Chart of Accounts
        /// - Non-cash transactions (e.g., depreciation, unrealised FX gains) are adjusted out in operating activities
        /// - The net change in cash must reconcile to the difference in opening and closing cash balances
        /// - Only posted journal entries within the specified period are included
        ///
        /// **Authorization:** Requires authenticated user with financial reporting permission
        /// </remarks>
        /// <param name="request">Query parameters including the period start/end dates and optional comparative period options.</param>
        /// <returns>The Cash Flow Statement with operating, investing, and financing activity sections and net cash change.</returns>
        /// <response code="200">Cash Flow Statement generated successfully</response>
        /// <response code="400">Validation failure - invalid date range or filter parameters</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("statements/cash-flow")]
        public async Task<IActionResult> GetCashFlowStatement([FromQuery] CashFlowStatementRequestDto request)
        {
            try
            {
                var cashFlowStatement = await _glService.GenerateCashFlowStatementAsync(request);
                return Ok(cashFlowStatement);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generates a Multi-Currency Detail Report showing transaction-level foreign currency activity with original and reporting currency amounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Reviewing all foreign currency transactions and their exchange rates for audit purposes
        /// - Reconciling foreign currency bank accounts and sub-ledger balances
        /// - Analysing realised and unrealised exchange rate gains and losses by account
        ///
        /// **Integration Pattern:**
        /// - Reads GL journal entry lines that involve non-base-currency transactions
        /// - Complements the Currency Revaluation endpoint by providing the underlying detail
        /// - Data feeds into the Balance Sheet and Income Statement foreign currency disclosures
        ///
        /// **Business Rules:**
        /// - Only accounts with foreign currency transactions are included
        /// - Each line shows the original currency amount, exchange rate, and reporting currency equivalent
        /// - The report highlights any variance between the posted rate and the current market rate
        /// - Only posted journal entries within the specified date range are included
        ///
        /// **Authorization:** Requires authenticated user with financial reporting permission
        /// </remarks>
        /// <param name="request">Query parameters including the date range, target currencies, and optional account filters.</param>
        /// <returns>The Multi-Currency Detail Report with per-transaction currency breakdowns and gain/loss analysis.</returns>
        /// <response code="200">Multi-Currency Detail Report generated successfully</response>
        /// <response code="400">Validation failure - invalid date range, currency, or filter parameters</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("statements/multi-currency-detail")]
        public async Task<IActionResult> GetMultiCurrencyDetailReport([FromQuery] MultiCurrencyDetailRequestDto request)
        {
            try
            {
                var report = await _glService.GenerateMultiCurrencyDetailReportAsync(request);
                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Validates whether a fiscal period is ready to be closed by checking for outstanding items and potential issues.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Running a pre-close checklist before committing to a period close
        /// - Identifying unposted or draft journal entries that need to be finalised
        /// - Detecting out-of-balance conditions or missing accrual entries
        ///
        /// **Integration Pattern:**
        /// - Should be called before the Close Period endpoint to surface warnings and blockers
        /// - Returns a validation result indicating readiness and any issues found
        /// - Does not modify any data; this is a read-only validation check
        ///
        /// **Business Rules:**
        /// - The period must exist and belong to the current tenant
        /// - Validation checks may include: unposted entries, sub-ledger reconciliation status, and trial balance integrity
        /// - A period can still be closed with warnings, but hard errors must be resolved first
        /// - Already-closed periods will return a validation indicating they are already closed
        ///
        /// **Authorization:** Requires authenticated user with period management permission
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal period to validate.</param>
        /// <returns>A validation result containing readiness status, warnings, and any blocking errors.</returns>
        /// <response code="200">Validation completed - check the result for readiness status and any issues</response>
        /// <response code="400">Validation failure - period not found or invalid state</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/validate-close")]
        public async Task<IActionResult> ValidatePeriodClose(Guid id)
        {
            try
            {
                var validation = await _glService.ValidatePeriodCloseAsync(id);
                return Ok(validation);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Closes a fiscal period, preventing any further journal entries from being posted to it.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Finalising a month-end or quarter-end close after all adjustments are posted
        /// - Enforcing the period close as part of the month-end close checklist
        /// - Preventing accidental postings to a completed period
        ///
        /// **Integration Pattern:**
        /// - Typically preceded by a call to Validate Period Close to ensure readiness
        /// - Once closed, any attempt to post a journal entry to this period will be rejected
        /// - The period can be reopened later via the Reopen Period endpoint if corrections are needed
        ///
        /// **Business Rules:**
        /// - The period must be in an open state to be closed
        /// - All sub-ledger postings for the period should be complete before closing
        /// - The route parameter ID is enforced as the authoritative period identifier
        /// - Closing a period does not automatically close the fiscal year
        ///
        /// **Authorization:** Requires authenticated user with period management permission
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal period to close.</param>
        /// <param name="request">The close request containing optional notes, closing adjustments, or confirmation flags.</param>
        /// <returns>The period close result including the updated period status and any closing entries generated.</returns>
        /// <response code="200">Period closed successfully</response>
        /// <response code="400">Validation failure - period already closed, locked, or has outstanding items</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/close")]
        public async Task<IActionResult> ClosePeriod(Guid id, [FromBody] PeriodCloseRequestDto request)
        {
            try
            {
                request.FiscalPeriodId = id; // Ensure ID from route is used
                var result = await _glService.CloseFiscalPeriodAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reopens a previously closed fiscal period, allowing journal entries to be posted to it again.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting an error discovered after period close that requires a new adjusting entry
        /// - Accommodating late-arriving invoices or transactions that must be recorded in the prior period
        /// - Reopening for audit adjustments requested by external auditors
        ///
        /// **Integration Pattern:**
        /// - Reverses the effect of the Close Period operation
        /// - Once reopened, journal entries can be posted to the period again
        /// - Financial statements will reflect any new entries posted to the reopened period
        ///
        /// **Business Rules:**
        /// - The period must be in a closed (but not permanently locked) state
        /// - A reason for reopening is typically required for the audit trail
        /// - The route parameter ID is enforced as the authoritative period identifier
        /// - Reopening a period in a closed fiscal year may require the year to be reopened first
        ///
        /// **Authorization:** Requires authenticated user with period management permission (elevated)
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal period to reopen.</param>
        /// <param name="request">The reopen request containing the reason for reopening and any authorisation details.</param>
        /// <returns>The period reopen result including the updated period status.</returns>
        /// <response code="200">Period reopened successfully</response>
        /// <response code="400">Validation failure - period not closed, permanently locked, or fiscal year is closed</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/reopen")]
        public async Task<IActionResult> ReopenPeriod(Guid id, [FromBody] PeriodReopenRequestDto request)
        {
            try
            {
                request.FiscalPeriodId = id;
                var result = await _glService.ReopenFiscalPeriodAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Locks a fiscal period, applying a hard lock that prevents both posting and reopening without an explicit unlock.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Permanently locking a period after external audit sign-off
        /// - Preventing accidental reopening of periods that have been reported to regulators
        /// - Applying an administrative hold during an investigation or review
        ///
        /// **Integration Pattern:**
        /// - A locked period is a stronger restriction than a closed period
        /// - The period must first be closed before it can be locked
        /// - Unlocking requires a separate explicit call to the Unlock Period endpoint
        ///
        /// **Business Rules:**
        /// - The period must be in a closed state before it can be locked
        /// - A lock reason is required and recorded in the audit trail
        /// - Locked periods reject all posting attempts and cannot be reopened until unlocked
        /// - Only users with elevated permissions should be able to lock periods
        ///
        /// **Authorization:** Requires authenticated user with period lock/unlock permission (elevated)
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal period to lock.</param>
        /// <param name="request">The lock request containing the reason for locking the period.</param>
        /// <returns>A confirmation message indicating the period was locked successfully.</returns>
        /// <response code="200">Period locked successfully</response>
        /// <response code="400">Validation failure - period not closed, already locked, or invalid state</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/lock")]
        public async Task<IActionResult> LockPeriod(Guid id, [FromBody] PeriodLockRequestDto request)
        {
            try
            {
                await _glService.LockFiscalPeriodAsync(id, request.LockReason);
                return Ok(new { message = "Period locked successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Unlocks a previously locked fiscal period, returning it to a closed (but modifiable) state.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing an administrative hold after a review or investigation is complete
        /// - Allowing post-audit adjustments to a period that was locked after audit sign-off
        /// - Correcting a period that was locked prematurely
        ///
        /// **Integration Pattern:**
        /// - Reverses the effect of the Lock Period operation, returning the period to a closed state
        /// - After unlocking, the period can be reopened via the Reopen Period endpoint if needed
        /// - The unlock action is recorded in the audit trail for compliance
        ///
        /// **Business Rules:**
        /// - The period must be in a locked state
        /// - An unlock reason is required and recorded in the audit trail
        /// - Unlocking does not automatically reopen the period; it returns to a closed state
        /// - This is a privileged operation requiring elevated authorization
        ///
        /// **Authorization:** Requires authenticated user with period lock/unlock permission (elevated)
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal period to unlock.</param>
        /// <param name="unlockReason">A string describing the reason for unlocking the period, recorded in the audit trail.</param>
        /// <returns>A confirmation message indicating the period was unlocked successfully.</returns>
        /// <response code="200">Period unlocked successfully</response>
        /// <response code="400">Validation failure - period not locked or invalid state</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("periods/{id}/unlock")]
        public async Task<IActionResult> UnlockPeriod(Guid id, [FromBody] string unlockReason)
        {
            try
            {
                await _glService.UnlockFiscalPeriodAsync(id, unlockReason);
                return Ok(new { message = "Period unlocked successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Closes an entire fiscal year, performing year-end closing entries and rolling forward retained earnings to the new fiscal year.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Executing the annual year-end close after all periods in the year have been closed
        /// - Generating the closing journal entry that zeroes out revenue and expense accounts into retained earnings
        /// - Finalising the fiscal year for statutory reporting and tax filing
        ///
        /// **Integration Pattern:**
        /// - All fiscal periods within the year should be closed before the year can be closed
        /// - Generates a year-end closing journal entry that debits/credits revenue and expense accounts to retained earnings
        /// - After year-end close, the next fiscal year's opening balances are established from the closing Balance Sheet
        ///
        /// **Business Rules:**
        /// - All fiscal periods in the year must be closed before the year can be closed
        /// - The year-end close generates an automatic journal entry transferring net income to retained earnings
        /// - Revenue and expense account balances are reset to zero for the new fiscal year
        /// - The route parameter ID is enforced as the authoritative fiscal year identifier
        /// - Year-end close is typically an irreversible operation requiring senior finance authorization
        ///
        /// **Authorization:** Requires authenticated user with fiscal year close permission (senior finance role)
        /// </remarks>
        /// <param name="id">The unique identifier of the fiscal year to close.</param>
        /// <param name="request">The year-end close request containing the retained earnings target account, optional notes, and confirmation flags.</param>
        /// <returns>The fiscal year close result including the generated closing journal entry and updated year status.</returns>
        /// <response code="200">Fiscal year closed successfully with closing entries generated</response>
        /// <response code="400">Validation failure - open periods exist, year already closed, or missing configuration</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("fiscal-years/{id}/close")]
        public async Task<IActionResult> CloseFiscalYear(Guid id, [FromBody] YearEndCloseRequestDto request)
        {
            try
            {
                request.FiscalYearId = id;
                var result = await _glService.CloseFiscalYearAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
