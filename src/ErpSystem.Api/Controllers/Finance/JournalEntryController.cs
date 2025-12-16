using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/journal-entries")]
    public class JournalEntryController : ControllerBase
    {
        private readonly IJournalEntryService _journalEntryService;

        public JournalEntryController(IJournalEntryService journalEntryService)
        {
            _journalEntryService = journalEntryService;
        }

        /// <summary>
        /// Retrieves journal entries with optional filtering by status, date range, and fiscal period.
        /// 
        /// **Common Use Cases:**
        /// - Audit trail review
        /// - Period-end reconciliation
        /// - Transaction history lookup
        /// - Integration monitoring (check if module transactions were posted)
        /// 
        /// **Integration Pattern:**
        /// - Filter by periodId to get all entries for a specific period
        /// - Filter by status="Posted" to see finalized transactions
        /// - Use date range to retrieve entries within a specific timeframe
        /// 
        /// **Business Rules:**
        /// - Only returns entries for the authenticated user's tenant
        /// - Includes all journal entry lines (debits and credits)
        /// - Results ordered by entry date descending (newest first)
        /// 
        /// **Authorization:** Requires Finance.Read permission
        /// </summary>
        /// <param name="status">Filter by posting status: Draft, Posted, Reversed</param>
        /// <param name="startDate">Filter entries on or after this date</param>
        /// <param name="endDate">Filter entries on or before this date</param>
        /// <param name="periodId">Filter entries in a specific fiscal period</param>
        /// <returns>List of journal entries with their transaction lines</returns>
        /// <response code="200">Returns the list of journal entries</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<List<JournalEntryDto>>> GetJournalEntries(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? periodId = null)
        {
            try
            {
                var entries = await _journalEntryService.GetJournalEntriesAsync();
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific journal entry by ID including all transaction lines.
        /// 
        /// **Common Use Cases:**
        /// - View transaction details
        /// - Verify posted transactions from other modules
        /// - Audit trail investigation
        /// 
        /// **Integration Pattern:**
        /// - Store the returned journal entry ID after posting
        /// - Use this endpoint to verify the entry was created correctly
        /// - Check posting status and balance
        /// 
        /// **Business Rules:**
        /// - Returns full entry details including all debit/credit lines
        /// - Includes posting metadata (posted by, posted date)
        /// - Shows reversal information if entry was reversed
        /// 
        /// **Authorization:** Requires Finance.Read permission
        /// </summary>
        /// <param name="id">Journal entry ID</param>
        /// <returns>Journal entry with all transaction lines</returns>
        /// <response code="200">Returns the journal entry details</response>
        /// <response code="404">Journal entry not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<JournalEntryDto>> GetJournalEntryById(Guid id)
        {
            try
            {
                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                return Ok(entry);
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
        /// Creates a new journal entry to post transactions to the General Ledger.
        /// **THIS IS THE PRIMARY INTEGRATION ENDPOINT FOR ALL ERP MODULES.**
        /// 
        /// **Module Integration Examples:**
        /// 
        /// **Inventory Module - COGS Posting:**
        /// ```
        /// POST /api/Finance/journal-entries
        /// {
        ///   "entryDate": "2024-12-15",
        ///   "description": "COGS for Sales Invoice INV-12345",
        ///   "referenceNumber": "INV-12345",
        ///   "sourceModule": "Inventory",
        ///   "fiscalPeriodId": "period-guid",
        ///   "transactions": [
        ///     { "accountId": "cogs-account-guid", "debitAmount": 5000, "description": "COGS" },
        ///     { "accountId": "inventory-account-guid", "creditAmount": 5000, "description": "Inventory reduction" }
        ///   ]
        /// }
        /// ```
        /// 
        /// **Sales Module - Revenue Recognition:**
        /// ```
        /// {
        ///   "entryDate": "2024-12-15",
        ///   "description": "Revenue for Invoice INV-12345",
        ///   "referenceNumber": "INV-12345",
        ///   "sourceModule": "Sales",
        ///   "transactions": [
        ///     { "accountId": "ar-account-guid", "debitAmount": 11500, "description": "Accounts Receivable" },
        ///     { "accountId": "revenue-account-guid", "creditAmount": 10000, "description": "Sales Revenue" },
        ///     { "accountId": "tax-account-guid", "creditAmount": 1500, "description": "VAT Payable" }
        ///   ]
        /// }
        /// ```
        /// 
        /// **Purchasing Module - Expense Accrual:**
        /// ```
        /// {
        ///   "entryDate": "2024-12-15",
        ///   "description": "Purchase Order PO-67890",
        ///   "referenceNumber": "PO-67890",
        ///   "sourceModule": "Purchasing",
        ///   "transactions": [
        ///     { "accountId": "expense-account-guid", "debitAmount": 8000, "description": "Office Supplies" },
        ///     { "accountId": "ap-account-guid", "creditAmount": 8000, "description": "Accounts Payable" }
        ///   ]
        /// }
        /// ```
        /// 
        /// **Payroll Module - Salary Posting:**
        /// ```
        /// {
        ///   "entryDate": "2024-12-31",
        ///   "description": "Payroll for December 2024",
        ///   "referenceNumber": "PAYROLL-DEC-2024",
        ///   "sourceModule": "Payroll",
        ///   "transactions": [
        ///     { "accountId": "salary-expense-guid", "debitAmount": 50000, "description": "Gross Salaries" },
        ///     { "accountId": "tax-withheld-guid", "creditAmount": 10000, "description": "Tax Withholding" },
        ///     { "accountId": "payroll-payable-guid", "creditAmount": 40000, "description": "Net Pay" }
        ///   ]
        /// }
        /// ```
        /// 
        /// **Fixed Assets Module - Depreciation:**
        /// ```
        /// {
        ///   "entryDate": "2024-12-31",
        ///   "description": "Monthly depreciation - December 2024",
        ///   "referenceNumber": "DEP-DEC-2024",
        ///   "sourceModule": "FixedAssets",
        ///   "transactions": [
        ///     { "accountId": "depreciation-expense-guid", "debitAmount": 2500, "description": "Depreciation Expense" },
        ///     { "accountId": "accumulated-dep-guid", "creditAmount": 2500, "description": "Accumulated Depreciation" }
        ///   ]
        /// }
        /// ```
        /// 
        /// **CRITICAL Business Rules:**
        /// 1. **Balanced Entry Required:** Total debits MUST equal total credits
        /// 2. **Fiscal Period Validation:** Entry date must fall within an open fiscal period
        /// 3. **Account Validation:** All account IDs must exist and allow direct posting
        /// 4. **Multi-Currency:** If posting foreign currency, include transactionCurrency and exchangeRate
        /// 5. **Source Module:** Always set sourceModule to identify the originating system
        /// 6. **Reference Number:** Include the source document reference for audit trail
        /// 
        /// **Integration Workflow:**
        /// 1. Validate fiscal period is open (GET /api/Finance/fiscal-periods)
        /// 2. Get GL account IDs (GET /api/Finance/accounts)
        /// 3. Get exchange rate if multi-currency (GET /api/Finance/exchange-rates/current/{code})
        /// 4. Create journal entry (POST /api/Finance/journal-entries)
        /// 5. Optionally post immediately (POST /api/Finance/journal-entries/{id}/post)
        /// 
        /// **Error Scenarios:**
        /// - 400: Unbalanced entry (debits ≠ credits)
        /// - 400: Invalid fiscal period (closed or not found)
        /// - 400: Invalid account (not found or posting not allowed)
        /// - 400: Missing required fields
        /// 
        /// **Authorization:** Requires Finance.Write permission
        /// </summary>
        /// <param name="dto">Journal entry creation details including all transaction lines</param>
        /// <returns>Created journal entry with generated ID and entry number</returns>
        /// <response code="201">Journal entry created successfully (status=Draft)</response>
        /// <response code="400">Invalid data or business rule violation</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        public async Task<ActionResult<JournalEntryDto>> CreateJournalEntry([FromBody] CreateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _journalEntryService.CreateJournalEntryAsync(dto);
                return CreatedAtAction(nameof(GetJournalEntryById), new { id = entry.Id }, entry);
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
        /// Updates a draft journal entry before it is posted.
        /// Typically used by Finance module for corrections. Other modules rarely need this.
        /// 
        /// **Business Rules:**
        /// - Can only update entries with status="Draft"
        /// - Cannot update posted or reversed entries
        /// - Updated entry must still be balanced
        /// - Cannot change fiscal period if entry is locked
        /// 
        /// **Integration Pattern:**
        /// - Most modules should create entries correctly the first time
        /// - Use this only for error correction before posting
        /// - Consider deleting and recreating instead if major changes needed
        /// 
        /// **Authorization:** Requires Finance.Write permission
        /// </summary>
        /// <param name="id">Journal entry ID to update</param>
        /// <param name="dto">Updated journal entry details</param>
        /// <returns>Updated journal entry</returns>
        /// <response code="200">Journal entry updated successfully</response>
        /// <response code="400">Cannot update - entry is posted or invalid data</response>
        /// <response code="404">Journal entry not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id}")]
        public async Task<ActionResult<JournalEntryDto>> UpdateJournalEntry(Guid id, [FromBody] UpdateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _journalEntryService.UpdateJournalEntryAsync(id, dto);
                return Ok(entry);
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
        /// Deletes a draft journal entry.
        /// Use this to cancel/remove entries that were created in error.
        /// 
        /// **Business Rules:**
        /// - Can only delete entries with status="Draft"
        /// - Cannot delete posted entries (use reverse instead)
        /// - Soft delete is performed (entry marked as deleted, not removed)
        /// 
        /// **Integration Pattern:**
        /// - Use this if entry creation failed validation after posting
        /// - For posted entries, use POST /api/Finance/journal-entries/{id}/reverse
        /// 
        /// **Authorization:** Requires Finance.Write permission
        /// </summary>
        /// <param name="id">Journal entry ID to delete</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Journal entry deleted successfully</response>
        /// <response code="400">Cannot delete - entry is posted</response>
        /// <response code="404">Journal entry not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteJournalEntry(Guid id)
        {
            try
            {
                await _journalEntryService.DeleteJournalEntryAsync(id);
                return NoContent();
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
        /// Posts a draft journal entry to the General Ledger, making it permanent and updating account balances.
        /// 
        /// **Common Use Cases:**
        /// - Finalize draft entries created by other modules
        /// - Post entries after approval workflow
        /// - Batch posting of multiple entries
        /// 
        /// **Integration Pattern:**
        /// - **Option 1 - Auto-Post:** Create and post in one workflow
        ///   1. POST /api/Finance/journal-entries (creates draft)
        ///   2. POST /api/Finance/journal-entries/{id}/post (posts immediately)
        /// 
        /// - **Option 2 - Manual Approval:** Create draft, wait for approval, then post
        ///   1. POST /api/Finance/journal-entries (creates draft)
        ///   2. Finance team reviews
        ///   3. POST /api/Finance/journal-entries/{id}/post (after approval)
        /// 
        /// **Business Rules:**
        /// - Entry must be balanced (debits = credits)
        /// - Fiscal period must be open
        /// - All accounts must allow direct posting
        /// - Cannot post already posted entries
        /// - Updates account balances immediately
        /// - Sets posting date to current date/time
        /// - Records posting user for audit trail
        /// 
        /// **Effects of Posting:**
        /// - Entry status changes from "Draft" to "Posted"
        /// - Account balances are updated
        /// - Entry becomes immutable (cannot be edited)
        /// - Can only be reversed, not deleted
        /// 
        /// **Authorization:** Requires Finance.Post permission
        /// </summary>
        /// <param name="id">Journal entry ID to post</param>
        /// <returns>Success message</returns>
        /// <response code="200">Journal entry posted successfully</response>
        /// <response code="400">Cannot post - entry is unbalanced, period closed, or already posted</response>
        /// <response code="404">Journal entry not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("{id}/post")]
        public async Task<ActionResult> PostJournalEntry(Guid id)
        {
            try
            {
                await _journalEntryService.PostJournalEntryAsync(id);
                return Ok(new { message = "Journal entry posted successfully" });
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
        /// Reverses a posted journal entry by creating an offsetting entry with opposite debits/credits.
        /// Use this to correct posted transactions from any module.
        /// 
        /// **Common Use Cases:**
        /// - Correct posting errors from other modules
        /// - Cancel transactions (e.g., voided invoices, returned purchases)
        /// - Period-end adjustments
        /// - Inventory adjustments
        /// 
        /// **Integration Examples:**
        /// 
        /// **Sales Module - Invoice Void:**
        /// - Original invoice posted journal entry
        /// - Customer cancels order
        /// - Call this endpoint to reverse the revenue recognition
        /// 
        /// **Inventory Module - Inventory Return:**
        /// - Original COGS entry posted
        /// - Goods returned to inventory
        /// - Call this endpoint to reverse COGS and restore inventory value
        /// 
        /// **Purchasing Module - Purchase Return:**
        /// - Original expense entry posted
        /// - Goods returned to supplier
        /// - Call this endpoint to reverse expense and AP
        /// 
        /// **How Reversal Works:**
        /// 1. Creates a new journal entry with opposite debits/credits
        /// 2. Links reversal entry to original entry
        /// 3. Marks original entry as "Reversed"
        /// 4. Posts reversal entry automatically
        /// 5. Updates account balances to reflect reversal
        /// 
        /// **Business Rules:**
        /// - Can only reverse posted entries
        /// - Cannot reverse already reversed entries
        /// - Reversal entry is created in the current open period
        /// - Original entry remains in the system (audit trail)
        /// - Reversal entry references the original entry ID
        /// 
        /// **Integration Pattern:**
        /// 1. Store original journal entry ID when posting transactions
        /// 2. When cancellation/return occurs, call this endpoint
        /// 3. Store the returned reversal entry ID
        /// 4. Update your module's transaction status
        /// 
        /// **Authorization:** Requires Finance.Post permission
        /// </summary>
        /// <param name="id">Journal entry ID to reverse</param>
        /// <returns>The newly created reversal journal entry</returns>
        /// <response code="200">Reversal entry created and posted successfully</response>
        /// <response code="400">Cannot reverse - entry not posted or already reversed</response>
        /// <response code="404">Journal entry not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("{id}/reverse")]
        public async Task<ActionResult<JournalEntryDto>> ReverseJournalEntry(Guid id)
        {
            try
            {
                var reversedEntry = await _journalEntryService.ReverseJournalEntryAsync(id, "Manual reversal");
                return Ok(reversedEntry);
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
    }
}
