using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages the Chart of Accounts (COA) for the General Ledger.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Setting up and maintaining the organization's Chart of Accounts structure
    /// - Creating segmented GL accounts using the segment-based account numbering scheme
    /// - Bulk-generating account combinations from segment value Cartesian products
    /// - Querying real-time account balances in any configured currency
    /// - Linking and managing multiple currencies on a single GL account for multi-currency operations
    ///
    /// **Integration Pattern:**
    /// - Works in conjunction with the Segment and SegmentValue controllers to define the COA structure
    /// - Account balances feed into Trial Balance, Financial Statements, and Reporting modules
    /// - Multi-currency links integrate with the Currency Revaluation service for period-end adjustments
    /// - Journal Entry posting references accounts managed by this controller
    /// - Fixed Assets, Accounts Payable, and Accounts Receivable sub-ledgers post to GL accounts defined here
    ///
    /// **Business Rules:**
    /// - Account codes and numbers are derived from segment values and must be unique across the COA
    /// - Accounts with posted transactions cannot be deleted; they must be inactivated instead
    /// - Each account has a base currency; additional currencies can be linked for multi-currency tracking
    /// - Bulk combination generation respects segment ordering (SegmentPosition) for account number construction
    ///
    /// **Authorization:** Requires authenticated user with Finance module access
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/accounts")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IGeneralLedgerService _glService;
        private readonly IAccountCombinationService _combinationService;

        public AccountController(
            IAccountService accountService,
            IGeneralLedgerService glService,
            IAccountCombinationService combinationService)
        {
            _accountService = accountService;
            _glService = glService;
            _combinationService = combinationService;
        }

        /// <summary>
        /// Creates a new segmented General Ledger account in the Chart of Accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adding a new expense, revenue, asset, liability, or equity account to the COA
        /// - Setting up department-specific or cost-center-specific GL accounts using segment values
        /// - Provisioning accounts required by new business activities or regulatory changes
        ///
        /// **Integration Pattern:**
        /// - Delegates to the General Ledger service which validates segment values and constructs the account number
        /// - The created account becomes available for Journal Entry posting, sub-ledger integration, and reporting
        /// - Returns a location header pointing to the account balance endpoint for the newly created account
        ///
        /// **Business Rules:**
        /// - Account code and account number must be unique; duplicates are rejected
        /// - All referenced segment values must exist and be active in their respective segments
        /// - The account type (Asset, Liability, Equity, Revenue, Expense) determines normal balance behavior
        /// - A valid currency code must be provided; it becomes the account's base currency
        ///
        /// **Authorization:** Requires authenticated user with account creation permission
        /// </remarks>
        /// <param name="accountDto">The account creation payload containing account name, type, segment values, and currency code.</param>
        /// <returns>The newly created account with its generated ID, account code, account number, name, type, and currency.</returns>
        /// <response code="201">Account created successfully; location header points to the balance endpoint.</response>
        /// <response code="400">Validation failure — null body, invalid segment values, duplicate account code, or invalid operation.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="500">Internal server error during account creation.</response>
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] AccountCreateDto accountDto)
        {
            try
            {
                if (accountDto == null)
                    return BadRequest(new { error = "Request body cannot be null" });

                var account = await _glService.CreateSegmentedAccountAsync(accountDto);

                return CreatedAtAction(
                    nameof(GetAccountBalance),
                    new { id = account.Id },
                    new
                    {
                        id = account.Id,
                        accountCode = account.AccountCode,
                        accountNumber = account.AccountNumber,
                        accountName = account.AccountName,
                        accountType = account.AccountType.ToString(),
                        currencyCode = account.CurrencyCode
                    });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message, parameter = ex.ParamName });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in CreateAccount: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred while creating the account", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves all GL accounts in the Chart of Accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating account picker dropdowns in Journal Entry, AP, AR, and Fixed Asset screens
        /// - Displaying the full Chart of Accounts list for review and administration
        /// - Exporting the COA for audit or external reporting purposes
        ///
        /// **Integration Pattern:**
        /// - Returns lightweight AccountDto projections suitable for list views and selection controls
        /// - Consumed by the Trial Balance, Financial Statements, and Budget modules to enumerate accounts
        /// - Frontend typically caches this list and refreshes on account creation or deletion events
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive accounts; consumers should filter by status as needed
        /// - Results include all account types (Asset, Liability, Equity, Revenue, Expense)
        /// - No pagination is applied; for very large COAs, consider filtering by segment or type
        ///
        /// **Authorization:** Requires authenticated user with Finance module read access
        /// </remarks>
        /// <returns>A collection of all GL accounts with their account code, name, type, and related metadata.</returns>
        /// <response code="200">Successfully retrieved all accounts.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> GetAllAccounts()
        {
            var accounts = await _accountService.GetAllAsync();
            return Ok(accounts);
        }

        /// <summary>
        /// Retrieves a single GL account by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing full account details on an account maintenance or inquiry screen
        /// - Loading account metadata before posting a journal entry line to validate the target account
        /// - Fetching account information for drill-down from Trial Balance or Financial Statement reports
        ///
        /// **Integration Pattern:**
        /// - Returns the complete AccountDto including segment breakdowns, currency, and status
        /// - Typically called after selecting an account from a list view or search result
        /// - Sub-ledger modules use this to validate that the target GL account exists and is active
        ///
        /// **Business Rules:**
        /// - Returns 404 if the account ID does not exist in the system
        /// - Inactive accounts are still retrievable by ID for historical reference and reporting
        ///
        /// **Authorization:** Requires authenticated user with Finance module read access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the GL account to retrieve.</param>
        /// <returns>The full account details including code, name, type, segments, and currency.</returns>
        /// <response code="200">Account found and returned successfully.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">No account exists with the specified ID.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<AccountDto>> GetAccountById(Guid id)
        {
            var account = await _accountService.GetByIdAsync(id);
            if (account == null)
            {
                return NotFound(new { error = $"Account with ID {id} not found" });
            }
            return Ok(account);
        }

        /// <summary>
        /// Deletes a GL account from the Chart of Accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing an account that was created in error before any transactions were posted
        /// - Cleaning up test or provisional accounts during initial COA setup
        /// - Deleting unused accounts as part of a COA restructuring initiative
        ///
        /// **Integration Pattern:**
        /// - Validates that no journal entries, sub-ledger transactions, or budgets reference the account
        /// - If the account has transaction history, an InvalidOperationException is thrown and deletion is refused
        /// - For accounts with history, use inactivation instead (handled via account update endpoints)
        ///
        /// **Business Rules:**
        /// - Accounts with posted transactions cannot be deleted; they must be inactivated
        /// - Deletion is permanent and cannot be undone
        /// - All currency links associated with the account are also removed upon deletion
        /// - System and control accounts (e.g., retained earnings, suspense) may be protected from deletion
        ///
        /// **Authorization:** Requires authenticated user with account deletion permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the GL account to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Account deleted successfully.</response>
        /// <response code="400">Account cannot be deleted due to existing transactions or business rule violation.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">No account exists with the specified ID.</response>
        /// <response code="500">Internal server error during deletion.</response>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(Guid id)
        {
            try
            {
                await _accountService.DeleteAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in DeleteAccount: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred while deleting the account", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the current balance of a GL account in the specified currency.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Displaying real-time account balances on the Chart of Accounts inquiry screen
        /// - Checking an account balance before posting a journal entry to validate sufficient funds or limits
        /// - Viewing account balances in foreign currencies for multi-currency reporting
        ///
        /// **Integration Pattern:**
        /// - Delegates to the General Ledger service which computes the balance from posted journal entry lines
        /// - Balance is calculated as the net of all debits and credits, respecting the account's normal balance direction
        /// - When a non-base currency is specified, the balance reflects translated amounts using the currency revaluation rates
        ///
        /// **Business Rules:**
        /// - The currency code defaults to the base currency configured in Finance multi-currency setup if not specified
        /// - Balance includes only posted (not draft or pending) journal entry lines
        /// - The balance reflects all periods up to and including the current open period
        /// - Requesting a balance in a currency not linked to the account may return zero or trigger a translation
        ///
        /// **Authorization:** Requires authenticated user with Finance module read access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the GL account.</param>
        /// <param name="currencyCode">The ISO 4217 currency code for the balance inquiry. Defaults to the configured finance base currency when omitted.</param>
        /// <returns>The account balance details including debit total, credit total, and net balance in the requested currency.</returns>
        /// <response code="200">Balance retrieved successfully.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">No account exists with the specified ID.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("{id}/balance")]
        public async Task<IActionResult> GetAccountBalance(Guid id, [FromQuery] string? currencyCode = null)
        {
            var balance = await _glService.GetAccountBalanceAsync(id, currencyCode);
            return Ok(balance);
        }

        #region Account Combination Generator

        /// <summary>
        /// Generates a preview of account combinations from selected segment values using a Cartesian product.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Previewing all possible GL account combinations before committing them to the COA
        /// - Validating that a proposed set of segment value selections will not produce duplicate accounts
        /// - Planning a bulk COA expansion by reviewing the generated account numbers and names
        ///
        /// **Integration Pattern:**
        /// - Uses the Account Combination service to compute the Cartesian product of selected segment values
        /// - Each combination is validated against existing accounts to flag duplicates
        /// - Respects the current segment ordering (SegmentPosition) for constructing account numbers and codes
        /// - The preview output is designed to feed directly into the bulk-create endpoint for confirmed combinations
        ///
        /// **Business Rules:**
        /// - At least one segment value must be selected for each required segment
        /// - Duplicate combinations (those already existing in the COA) are flagged but still included in the preview
        /// - The generated account number follows the configured segment separator and ordering
        /// - No accounts are created during preview; this is a read-only projection operation
        ///
        /// **Authorization:** Requires authenticated user with account creation permission
        /// </remarks>
        /// <param name="request">The combination request containing segment value selections and account properties (type, currency, etc.).</param>
        /// <returns>A list of previewed account combinations, each with its generated account number, name, and validation status indicating whether it is a duplicate.</returns>
        /// <response code="200">Combinations generated and returned successfully.</response>
        /// <response code="400">Validation failure — null body, missing segment selections, or invalid configuration.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="500">Internal server error during combination generation.</response>
        [HttpPost("combinations/preview")]
        public async Task<ActionResult<List<AccountCombinationPreviewDto>>> PreviewCombinations(
            [FromBody] CombinationRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body cannot be null" });

                var previews = await _combinationService.GenerateCombinationsAsync(request);
                return Ok(previews);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in PreviewCombinations: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred while generating combinations", details = ex.Message });
            }
        }

        /// <summary>
        /// Bulk-creates GL accounts from a set of previously previewed account combinations.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Committing a batch of new GL accounts after reviewing the combination preview results
        /// - Rapidly expanding the COA when new departments, cost centers, or projects are introduced
        /// - Automating COA setup during initial system configuration or chart restructuring
        ///
        /// **Integration Pattern:**
        /// - Accepts the output of the preview endpoint (or a filtered subset) as its input
        /// - Delegates to the Account Combination service which creates each account via the GL service
        /// - Returns a summary result containing counts of created accounts, skipped duplicates, and any errors
        /// - Newly created accounts are immediately available for journal entry posting and sub-ledger integration
        ///
        /// **Business Rules:**
        /// - Only valid (non-duplicate) combinations are created; duplicates are skipped by default
        /// - At least one combination must be provided in the request
        /// - Each combination must reference valid, active segment values
        /// - The operation is not atomic — partial success is possible; the result details which combinations succeeded or failed
        ///
        /// **Authorization:** Requires authenticated user with account creation permission
        /// </remarks>
        /// <param name="request">The bulk creation request containing a list of account combinations to create, typically sourced from the preview endpoint.</param>
        /// <returns>A result summary including the count of successfully created accounts, skipped duplicates, and any individual errors.</returns>
        /// <response code="200">Bulk creation completed; result summary returned with success and failure details.</response>
        /// <response code="400">Validation failure — null body, empty combination list, or invalid operation.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="500">Internal server error during bulk creation.</response>
        [HttpPost("combinations/bulk-create")]
        public async Task<ActionResult<BulkCreationResultDto>> BulkCreateAccounts(
            [FromBody] BulkCreateAccountsRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body cannot be null" });

                if (!request.Combinations.Any())
                    return BadRequest(new { error = "No combinations provided" });

                var result = await _combinationService.BulkCreateAccountsAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in BulkCreateAccounts: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred during bulk creation", details = ex.Message });
            }
        }

        #endregion

        #region Multi-Currency Management

        /// <summary>
        /// Links an additional currency to an existing GL account for multi-currency transaction tracking.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Enabling a bank account to track balances in a foreign currency (e.g., USD, EUR, GBP)
        /// - Adding currency support to AP/AR control accounts that process foreign vendor or customer invoices
        /// - Configuring intercompany accounts to handle transactions in the subsidiary's local currency
        ///
        /// **Integration Pattern:**
        /// - Creates a currency link record associating the specified currency with the GL account
        /// - Once linked, journal entries can post amounts in the linked currency to this account
        /// - The Currency Revaluation service uses these links to determine which accounts need period-end revaluation
        /// - Returns a location header pointing to the account's currency links list endpoint
        ///
        /// **Business Rules:**
        /// - The account ID in the URL path must match the account ID in the request body
        /// - The currency must be a valid, active currency defined in the system's currency master
        /// - A currency cannot be linked to the same account more than once; duplicates are rejected
        /// - The account's base currency is implicitly linked and does not need to be added separately
        ///
        /// **Authorization:** Requires authenticated user with account configuration permission
        /// </remarks>
        /// <param name="accountId">The unique identifier (GUID) of the GL account to link the currency to.</param>
        /// <param name="dto">The currency link payload containing the account ID and the ISO 4217 currency code to link.</param>
        /// <returns>The created currency link details including account ID, currency code, and link status.</returns>
        /// <response code="201">Currency linked successfully; location header points to the account's currency links.</response>
        /// <response code="400">Validation failure — account ID mismatch or currency already linked.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">Account or currency not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("{accountId}/currencies")]
        public async Task<ActionResult<CurrencyLinkDto>> AddCurrencyLink(Guid accountId, [FromBody] AddCurrencyLinkDto dto)
        {
            if (accountId != dto.AccountId)
                return BadRequest("Account ID mismatch");

            try
            {
                var result = await _accountService.AddCurrencyLinkAsync(dto);
                return CreatedAtAction(nameof(GetAccountCurrencyLinks), new { accountId }, result);
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
        /// Removes a currency link from a GL account, with optional transaction history protection override.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing a currency that was linked in error before any transactions were posted in that currency
        /// - Force-removing a currency link during COA cleanup when the currency is no longer in use
        /// - Decommissioning a foreign currency from an account after migrating transactions to a different account
        ///
        /// **Integration Pattern:**
        /// - Checks for existing transactions posted in the specified currency on the account
        /// - By default, refuses removal if transaction history exists; use forceRemove to override
        /// - When force-removed, a reason must be provided for the audit trail
        /// - After removal, new journal entries can no longer post in the removed currency to this account
        ///
        /// **Business Rules:**
        /// - The account's base currency cannot be removed
        /// - If transactions exist in the currency, removal is blocked unless forceRemove is set to true
        /// - Force removal requires a reason for audit compliance
        /// - Removal is permanent; to temporarily disable, use the inactivate endpoint instead
        ///
        /// **Authorization:** Requires authenticated user with account configuration permission
        /// </remarks>
        /// <param name="accountId">The unique identifier (GUID) of the GL account.</param>
        /// <param name="currencyCode">The ISO 4217 currency code to remove from the account.</param>
        /// <param name="forceRemove">When true, allows removal even if transaction history exists. Defaults to false.</param>
        /// <param name="reason">Required when forceRemove is true. An audit-trail reason explaining why the currency link is being force-removed.</param>
        /// <returns>The removal result including whether the removal succeeded, any warnings, and transaction impact details.</returns>
        /// <response code="200">Currency link removed successfully; result includes removal details.</response>
        /// <response code="400">Cannot remove — transactions exist and forceRemove is not enabled.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">Account or currency link not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("{accountId}/currencies/{currencyCode}")]
        public async Task<ActionResult<CurrencyLinkRemovalResultDto>> RemoveCurrencyLink(
            Guid accountId,
            string currencyCode,
            [FromQuery] bool forceRemove = false,
            [FromQuery] string? reason = null)
        {
            try
            {
                var dto = new RemoveCurrencyLinkDto
                {
                    AccountId = accountId,
                    CurrencyCode = currencyCode,
                    ForceRemove = forceRemove,
                    Reason = reason
                };

                var result = await _accountService.RemoveCurrencyLinkAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Inactivates a currency link on a GL account without removing it (soft delete).
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Temporarily disabling a foreign currency on an account while retaining historical transaction data
        /// - Preventing new transactions in a specific currency without losing the link's audit trail
        /// - Phasing out a currency from an account as part of a gradual currency consolidation
        ///
        /// **Integration Pattern:**
        /// - Sets the currency link status to inactive, preserving the link record and all historical data
        /// - Inactive currency links are excluded from active currency dropdowns in journal entry screens
        /// - The Currency Revaluation service skips inactive links during period-end processing
        /// - The link can be reactivated later if the currency needs to be restored on the account
        ///
        /// **Business Rules:**
        /// - The account's base/primary currency link cannot be inactivated
        /// - Already-inactive links return the current state without error
        /// - Historical transactions in the inactivated currency remain intact and reportable
        /// - No new transactions can be posted in the inactivated currency until the link is reactivated
        ///
        /// **Authorization:** Requires authenticated user with account configuration permission
        /// </remarks>
        /// <param name="accountId">The unique identifier (GUID) of the GL account.</param>
        /// <param name="currencyCode">The ISO 4217 currency code of the currency link to inactivate.</param>
        /// <returns>The updated currency link details reflecting the inactive status.</returns>
        /// <response code="200">Currency link inactivated successfully.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">Account or currency link not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPatch("{accountId}/currencies/{currencyCode}/inactivate")]
        public async Task<ActionResult<CurrencyLinkDto>> InactivateCurrencyLink(Guid accountId, string currencyCode)
        {
            try
            {
                var result = await _accountService.InactivateCurrencyLinkAsync(accountId, currencyCode);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Retrieves all currency links configured for a GL account, with optional inclusion of inactive links.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the currency dropdown when posting a journal entry line to a specific account
        /// - Displaying the full multi-currency configuration of an account on the account maintenance screen
        /// - Auditing which currencies are active or inactive on an account for compliance review
        ///
        /// **Integration Pattern:**
        /// - Returns the list of currency links associated with the account, filtered by active status by default
        /// - The Journal Entry screen uses this to restrict currency selection to valid linked currencies
        /// - The Currency Revaluation module queries active links to determine which account-currency pairs need revaluation
        /// - Account inquiry screens use this with includeInactive=true to show the complete currency history
        ///
        /// **Business Rules:**
        /// - By default, only active currency links are returned (includeInactive defaults to false)
        /// - Setting includeInactive to true returns both active and inactive links
        /// - The account's base currency is always included as an active link
        /// - Each link includes the currency code, status, and link metadata
        ///
        /// **Authorization:** Requires authenticated user with Finance module read access
        /// </remarks>
        /// <param name="accountId">The unique identifier (GUID) of the GL account.</param>
        /// <param name="includeInactive">When true, includes inactive currency links in the response. Defaults to false.</param>
        /// <returns>A list of currency links for the account, each containing currency code, active status, and link details.</returns>
        /// <response code="200">Currency links retrieved successfully.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">Account not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("{accountId}/currencies")]
        public async Task<ActionResult<List<CurrencyLinkDto>>> GetAccountCurrencyLinks(
            Guid accountId,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                var result = await _accountService.GetAccountCurrencyLinksAsync(accountId, includeInactive);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        #endregion
    }
}
