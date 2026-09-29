using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Manages bank account records for Cash Management, Bank Reconciliation, and Payment Processing.
/// </summary>
/// <remarks>
/// Provides CRUD operations, balance inquiries, and transaction history for all configured bank accounts.
///
/// **Key Integrations:**
/// - Cash Transaction endpoints for receipts, payments, and transfers
/// - Bank Reconciliation endpoints for statement matching
/// - General Ledger for automatic journal posting
/// </remarks>
[ApiController]
[Authorize]
[Route("api/finance/bank-accounts")]
public class BankAccountController : ControllerBase
{
    private readonly IBankAccountService _bankAccountService;

    public BankAccountController(IBankAccountService bankAccountService)
    {
        _bankAccountService = bankAccountService;
    }

    /// <summary>
    /// Retrieves all bank accounts configured in the system.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Populating bank account dropdown lists in payment forms
    /// - Administrative review of all banking relationships
    /// - Cash management dashboard overview
    ///
    /// **Integration Pattern:**
    /// - Use before creating cash transactions to get valid bank account IDs
    /// - Combine with balance endpoint for a full cash position report
    ///
    /// **Business Rules:**
    /// - Returns both active and inactive accounts
    /// - Use GET /active for only operational accounts
    /// </remarks>
    /// <returns>List of all bank accounts</returns>
    /// <response code="200">Returns the complete list of bank accounts</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IEnumerable<BankAccountDto>>> GetAll()
    {
        var accounts = await _bankAccountService.GetAllAsync();
        return Ok(accounts);
    }

    /// <summary>
    /// Retrieves only active bank accounts available for transactions.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Populating bank account selectors in payment and receipt forms
    /// - Filtering available accounts for cash transfers
    ///
    /// **Integration Pattern:**
    /// - Preferred over GET /all when building transaction entry forms
    /// - Use these IDs when creating cash transactions or bank reconciliations
    ///
    /// **Business Rules:**
    /// - Excludes closed or suspended accounts
    /// - Only active accounts can receive new transactions
    /// </remarks>
    /// <returns>List of active bank accounts</returns>
    /// <response code="200">Returns active bank accounts only</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("active")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IEnumerable<BankAccountDto>>> GetActive()
    {
        var accounts = await _bankAccountService.GetActiveAccountsAsync();
        return Ok(accounts);
    }

    /// <summary>
    /// Retrieves a specific bank account by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Viewing bank account details on an account detail page
    /// - Pre-populating edit forms with current account data
    ///
    /// **Business Rules:**
    /// - Returns full account details including bank name, account number, currency, and GL mapping
    /// </remarks>
    /// <param name="id">The unique identifier of the bank account</param>
    /// <returns>The bank account details</returns>
    /// <response code="200">Returns the bank account</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Bank account not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<BankAccountDto>> GetById(Guid id)
    {
        var account = await _bankAccountService.GetByIdAsync(id);
        if (account == null)
            return NotFound();

        return Ok(account);
    }

    /// <summary>
    /// Retrieves the current balance for a specific bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Cash position reporting and treasury dashboards
    /// - Pre-validation before processing large payments
    /// - Bank reconciliation starting balance reference
    ///
    /// **Integration Pattern:**
    /// - Call before creating payments to verify sufficient funds
    /// - Use in combination with GET /all for a consolidated cash position view
    ///
    /// **Business Rules:**
    /// - Balance reflects all posted transactions up to the current date
    /// - Includes both book balance and available balance if applicable
    /// </remarks>
    /// <param name="id">The unique identifier of the bank account</param>
    /// <returns>The bank account balance details</returns>
    /// <response code="200">Returns the current balance</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Bank account not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}/balance")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<BankAccountBalanceDto>> GetBalance(Guid id)
    {
        try
        {
            var balance = await _bankAccountService.GetBalanceAsync(id);
            return Ok(balance);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves transaction history for a specific bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Bank reconciliation — compare system transactions with bank statement
    /// - Auditing cash movements for a specific account
    /// - Generating bank account statements for a date range
    ///
    /// **Integration Pattern:**
    /// - Use with Bank Reconciliation endpoints to match transactions against statement lines
    /// - Filter by date range matching the bank statement period
    ///
    /// **Business Rules:**
    /// - Returns transactions ordered by date
    /// - If no date filters provided, returns all transactions for the account
    /// - Includes receipts, payments, and transfers involving this account
    /// </remarks>
    /// <param name="id">The unique identifier of the bank account</param>
    /// <param name="fromDate">Optional start date filter (inclusive)</param>
    /// <param name="toDate">Optional end date filter (inclusive)</param>
    /// <returns>List of cash transactions for the bank account</returns>
    /// <response code="200">Returns the transaction list</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Bank account not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}/transactions")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IEnumerable<CashTransactionDto>>> GetTransactions(
        Guid id,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var transactions = await _bankAccountService.GetTransactionsAsync(id, fromDate, toDate);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new bank account record in the system.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Setting up a new banking relationship
    /// - Adding petty cash or mobile money accounts
    /// - Configuring accounts for multi-currency operations
    ///
    /// **Integration Pattern:**
    /// - After creation, link the account to a GL account for automatic journal posting
    /// - The returned ID is used in Cash Transaction and Bank Reconciliation endpoints
    ///
    /// **Business Rules:**
    /// - Account number must be unique within the same bank
    /// - A corresponding GL account mapping is required for posting transactions
    /// - Currency must be a valid configured currency in the system
    /// - New accounts default to active status
    /// </remarks>
    /// <param name="dto">The bank account details to create</param>
    /// <returns>The newly created bank account</returns>
    /// <response code="201">Bank account created successfully</response>
    /// <response code="400">Validation error — duplicate account number, invalid currency, or missing required fields</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageBankAccounts)]
    public async Task<ActionResult<BankAccountDto>> Create([FromBody] CreateBankAccountDto dto)
    {
        try
        {
            var account = await _bankAccountService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing bank account's details.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Correcting bank account information (branch, contact details)
    /// - Updating GL account mapping
    /// - Changing account status (active/inactive)
    ///
    /// **Business Rules:**
    /// - Cannot change the currency of an account that has existing transactions
    /// - Account number changes may require reconciliation review
    /// - Deactivating an account prevents new transactions but preserves history
    /// </remarks>
    /// <param name="id">The unique identifier of the bank account to update</param>
    /// <param name="dto">The updated bank account details</param>
    /// <returns>The updated bank account</returns>
    /// <response code="200">Bank account updated successfully</response>
    /// <response code="400">Validation error — invalid data or business rule violation</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Bank account not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPut("{id}")]
    [Authorize(Policy = FinancePermissions.ManageBankAccounts)]
    public async Task<ActionResult<BankAccountDto>> Update(Guid id, [FromBody] UpdateBankAccountDto dto)
    {
        try
        {
            var account = await _bankAccountService.UpdateAsync(id, dto);
            return Ok(account);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a bank account from the system.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Removing erroneously created accounts
    /// - Cleaning up test data
    ///
    /// **Business Rules:**
    /// - Cannot delete an account that has existing transactions — deactivate instead
    /// - Cannot delete an account referenced in pending bank reconciliations
    /// - Deletion is permanent and cannot be undone
    ///
    /// **Authorization:** Requires Finance admin permission
    /// </remarks>
    /// <param name="id">The unique identifier of the bank account to delete</param>
    /// <response code="204">Bank account deleted successfully</response>
    /// <response code="400">Cannot delete — account has existing transactions or reconciliations</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Bank account not found</response>
    /// <response code="500">Internal server error</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = FinancePermissions.ManageBankAccounts)]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _bankAccountService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
