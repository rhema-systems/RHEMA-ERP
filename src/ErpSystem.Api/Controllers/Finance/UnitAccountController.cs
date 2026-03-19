using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Accounts.
    /// </summary>
    /// <remarks>
    /// Unit Accounts are hierarchical accounts for tracking non-financial quantities.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/unit-accounts")]
    public class UnitAccountController : ControllerBase
    {
        private readonly IUnitAccountService _unitAccountService;

        public UnitAccountController(IUnitAccountService unitAccountService)
        {
            _unitAccountService = unitAccountService;
        }

        /// <summary>
        /// Retrieves all unit accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading all unit accounts for display in a flat list or table
        /// - Populating dropdown selectors for account selection
        /// - Exporting the full list of unit accounts for reporting
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint to retrieve all accounts regardless of hierarchy or type
        /// - Use the hierarchy endpoint if a tree-structured response is needed
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive accounts
        /// - Accounts are returned in a flat list without parent-child nesting
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all unit accounts</returns>
        /// <response code="200">Returns the list of unit accounts</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<List<UnitAccountDto>>> GetUnitAccounts()
        {
            try
            {
                var accounts = await _unitAccountService.GetAllAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves unit accounts in a hierarchical structure.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Rendering a tree view of unit accounts in the UI
        /// - Displaying parent-child relationships between accounts
        /// - Building navigation structures for account browsing
        ///
        /// **Integration Pattern:**
        /// - Returns top-level accounts with nested children populated recursively
        /// - Use the flat list endpoint if hierarchy is not needed
        ///
        /// **Business Rules:**
        /// - Each account appears only once in the hierarchy
        /// - Root-level accounts have no parent
        /// - Child accounts are nested within their parent's children collection
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>Hierarchically structured list of unit accounts</returns>
        /// <response code="200">Returns the hierarchical list of unit accounts</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("hierarchy")]
        public async Task<ActionResult<List<UnitAccountHierarchyDto>>> GetAccountHierarchy()
        {
            try
            {
                var hierarchy = await _unitAccountService.GetHierarchyAsync();
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves unit accounts filtered by unit type.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Listing all accounts associated with a specific unit type (e.g., membership, attendance)
        /// - Filtering accounts in a UI based on the selected unit type
        /// - Building type-specific reports or dashboards
        ///
        /// **Integration Pattern:**
        /// - Provide the unit type GUID as a route parameter
        /// - Returns only accounts that belong to the specified unit type
        ///
        /// **Business Rules:**
        /// - Returns an empty list if no accounts match the given unit type
        /// - The unit type ID must be a valid GUID
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="unitTypeId">The unique identifier of the unit type to filter by</param>
        /// <returns>List of unit accounts matching the specified unit type</returns>
        /// <response code="200">Returns the filtered list of unit accounts</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("by-type/{unitTypeId:guid}")]
        public async Task<ActionResult<List<UnitAccountDto>>> GetByUnitType(Guid unitTypeId)
        {
            try
            {
                var accounts = await _unitAccountService.GetByUnitTypeAsync(unitTypeId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves only posting accounts (leaf-level accounts that accept journal entries).
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating account selectors in journal entry forms
        /// - Listing accounts that can receive direct postings
        /// - Validating that a selected account is eligible for transactions
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when building journal entry or transaction forms
        /// - Only accounts returned here should be used as targets for postings
        ///
        /// **Business Rules:**
        /// - Only leaf-level (non-parent) accounts are returned
        /// - Parent/header accounts that aggregate child balances are excluded
        /// - Posting accounts are the only accounts that accept direct journal entries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of posting-eligible unit accounts</returns>
        /// <response code="200">Returns the list of posting accounts</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("posting")]
        public async Task<ActionResult<List<UnitAccountDto>>> GetPostingAccounts()
        {
            try
            {
                var accounts = await _unitAccountService.GetPostingAccountsAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific unit account with detail and balance history.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing full details of a specific unit account
        /// - Displaying account information alongside its balance history
        /// - Loading account data for editing or review
        ///
        /// **Integration Pattern:**
        /// - Pass the account's unique GUID as a route parameter
        /// - Returns enriched detail including balance history not available in list endpoints
        ///
        /// **Business Rules:**
        /// - Returns 404 if the account does not exist
        /// - Includes balance history and detailed metadata beyond the basic DTO
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account</param>
        /// <returns>The unit account detail with balance history</returns>
        /// <response code="200">Returns the unit account detail</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UnitAccountDetailDto>> GetUnitAccountById(Guid id)
        {
            try
            {
                var account = await _unitAccountService.GetByIdAsync(id);
                if (account == null)
                    return NotFound($"Unit account with ID {id} not found");

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a unit account by its account number.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up an account using a human-readable account number
        /// - Resolving an account number entered by a user in a search field
        /// - Integrating with external systems that reference accounts by number
        ///
        /// **Integration Pattern:**
        /// - Pass the account number as a route parameter (string)
        /// - Returns the matching account or 404 if not found
        ///
        /// **Business Rules:**
        /// - Account numbers must match exactly (case-sensitive)
        /// - Returns 404 if no account matches the given number
        /// - Each account number is unique across the system
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="accountNumber">The account number to look up</param>
        /// <returns>The unit account matching the given account number</returns>
        /// <response code="200">Returns the matching unit account</response>
        /// <response code="404">Unit account with the specified number not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("number/{accountNumber}")]
        public async Task<ActionResult<UnitAccountDto>> GetByAccountNumber(string accountNumber)
        {
            try
            {
                var account = await _unitAccountService.GetByAccountNumberAsync(accountNumber);
                if (account == null)
                    return NotFound($"Unit account with number {accountNumber} not found");

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adding a new unit account to the chart of accounts
        /// - Setting up sub-accounts under an existing parent account
        /// - Creating posting accounts for new tracking categories
        ///
        /// **Integration Pattern:**
        /// - Submit a CreateUnitAccountDto in the request body
        /// - Returns the created account with a 201 status and a Location header
        /// - The Location header points to the GetUnitAccountById endpoint for the new account
        ///
        /// **Business Rules:**
        /// - Account number must be unique across the system
        /// - If a parent account ID is specified, the parent must exist
        /// - Validation errors (duplicate numbers, invalid parent) return 400
        /// - Model validation is enforced on the request body
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The unit account creation data</param>
        /// <returns>The newly created unit account</returns>
        /// <response code="201">Unit account created successfully</response>
        /// <response code="400">Validation error or business rule violation</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        public async Task<ActionResult<UnitAccountDto>> CreateUnitAccount([FromBody] CreateUnitAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var account = await _unitAccountService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetUnitAccountById), new { id = account.Id }, account);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Modifying the name or description of an existing unit account
        /// - Changing the parent account to reorganize the hierarchy
        /// - Updating account metadata such as unit type association
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter and the update data in the request body
        /// - Returns the updated account on success
        ///
        /// **Business Rules:**
        /// - The account must exist; returns 404 if not found
        /// - Model validation is enforced on the request body
        /// - Certain fields may be restricted from modification if the account has posted transactions
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account to update</param>
        /// <param name="dto">The updated unit account data</param>
        /// <returns>The updated unit account</returns>
        /// <response code="200">Unit account updated successfully</response>
        /// <response code="400">Validation error</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UnitAccountDto>> UpdateUnitAccount(Guid id, [FromBody] UpdateUnitAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var account = await _unitAccountService.UpdateAsync(id, dto);
                return Ok(account);
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
        /// Deletes a unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing an unused or erroneously created unit account
        /// - Cleaning up accounts that are no longer needed
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Returns 204 No Content on successful deletion
        ///
        /// **Business Rules:**
        /// - The account must exist; returns 404 if not found
        /// - Accounts with existing balances or posted journal entries cannot be deleted (returns 400)
        /// - Parent accounts with children may not be deletable until children are removed
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account to delete</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit account deleted successfully</response>
        /// <response code="400">Account cannot be deleted due to business rule constraints</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.DeleteAsync(id);
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
        /// Activates a unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Re-enabling a previously deactivated unit account
        /// - Making an account available for postings and transactions again
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Returns 204 No Content on success
        /// - Use in conjunction with the deactivate endpoint for lifecycle management
        ///
        /// **Business Rules:**
        /// - The account must exist; returns 404 if not found
        /// - Activating an already-active account is a no-op
        /// - Once activated, the account becomes eligible for journal entry postings
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account to activate</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit account activated successfully</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPatch("{id:guid}/activate")]
        public async Task<ActionResult> ActivateUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.ActivateAsync(id);
                return NoContent();
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
        /// Deactivates a unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Temporarily disabling an account without deleting it
        /// - Preventing further postings to an account that is being phased out
        /// - Soft-closing an account at the end of a fiscal period
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Returns 204 No Content on success
        /// - Use in conjunction with the activate endpoint for lifecycle management
        ///
        /// **Business Rules:**
        /// - The account must exist; returns 404 if not found
        /// - Deactivating an already-inactive account is a no-op
        /// - Deactivated accounts cannot receive new journal entry postings
        /// - Existing balances and history are preserved
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account to deactivate</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit account deactivated successfully</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPatch("{id:guid}/deactivate")]
        public async Task<ActionResult> DeactivateUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.DeactivateAsync(id);
                return NoContent();
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
        /// Gets the current balance for a unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Displaying the live balance of an account on a dashboard
        /// - Checking the current quantity tracked by a specific unit account
        /// - Validating account balances before performing operations
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Returns the current balance as a decimal value
        /// - For full balance history, use the balances endpoint instead
        ///
        /// **Business Rules:**
        /// - Returns the most up-to-date balance based on posted journal entries
        /// - Balance reflects the net of all debit and credit postings
        /// - Unposted or draft entries are not included in the balance
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account</param>
        /// <returns>The current balance of the unit account</returns>
        /// <response code="200">Returns the current balance</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id:guid}/balance")]
        public async Task<ActionResult<decimal>> GetCurrentBalance(Guid id)
        {
            try
            {
                var balance = await _unitAccountService.GetCurrentBalanceAsync(id);
                return Ok(balance);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets balance history for a unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing periodic balance snapshots for trend analysis
        /// - Generating balance reports across fiscal years
        /// - Auditing historical balance changes over time
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Optionally filter by fiscal year using the fiscalYearId query parameter
        /// - Returns a list of balance records across periods
        ///
        /// **Business Rules:**
        /// - If fiscalYearId is omitted, balances for all fiscal years are returned
        /// - If fiscalYearId is provided, only balances for that fiscal year are returned
        /// - Balance records are based on posted journal entries only
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account</param>
        /// <param name="fiscalYearId">Optional fiscal year ID to filter balances by a specific fiscal year</param>
        /// <returns>List of balance records for the unit account</returns>
        /// <response code="200">Returns the balance history</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id:guid}/balances")]
        public async Task<ActionResult<List<UnitAccountBalanceDto>>> GetBalances(Guid id, [FromQuery] Guid? fiscalYearId = null)
        {
            try
            {
                var balances = await _unitAccountService.GetBalancesAsync(id, fiscalYearId);
                return Ok(balances);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recalculates balances for a unit account based on posted journal entries.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting balance discrepancies caused by data inconsistencies
        /// - Rebuilding balances after bulk data imports or migrations
        /// - Administrative maintenance to ensure balance accuracy
        ///
        /// **Integration Pattern:**
        /// - Pass the account GUID as a route parameter
        /// - Returns 204 No Content on successful recalculation
        /// - This is an administrative operation and should be used sparingly
        ///
        /// **Business Rules:**
        /// - The account must exist; returns 404 if not found
        /// - Recalculation reprocesses all posted journal entries for the account
        /// - Only posted (not draft or voided) entries are considered
        /// - This operation may take time for accounts with large transaction volumes
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the unit account to recalculate</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Balances recalculated successfully</response>
        /// <response code="404">Unit account not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("{id:guid}/recalculate")]
        public async Task<ActionResult> RecalculateBalances(Guid id)
        {
            try
            {
                await _unitAccountService.RecalculateBalancesAsync(id);
                return NoContent();
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
    }
}
