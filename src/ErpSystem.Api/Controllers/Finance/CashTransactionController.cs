using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Manages cash transactions including receipts, payments, and inter-bank transfers.
/// </summary>
/// <remarks>
/// **Domain Responsibility:**
/// This controller serves as the primary entry point for all cash-based financial
/// movements. It handles the recording of money received (receipts), money paid out
/// (payments), and the movement of funds between the organization's own bank accounts
/// (transfers). Each transaction is linked to a specific bank account and generates
/// corresponding journal entries in the general ledger.
///
/// **Integration Points:**
/// - **Bank Accounts:** Every cash transaction is associated with a bank account from
///   the bank account register.
/// - **General Ledger:** Receipts, payments, and transfers automatically create double-entry
///   journal postings to maintain ledger integrity.
/// - **Bank Reconciliation:** Transactions carry a reconciliation status flag used by the
///   bank reconciliation module to match against bank statements.
/// - **Accounts Receivable / Payable:** Receipts and payments may settle outstanding
///   customer invoices or vendor bills respectively.
///
/// **Authorization:** Requires Finance module permissions.
/// </remarks>
[ApiController]
[Route("api/finance/cash-transactions")]
public class CashTransactionController : ControllerBase
{
    private readonly ICashTransactionService _cashTransactionService;

    public CashTransactionController(ICashTransactionService cashTransactionService)
    {
        _cashTransactionService = cashTransactionService;
    }

    /// <summary>
    /// Retrieves all cash transactions, optionally filtered by a date range.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Displaying the full cash transaction register in the finance dashboard
    /// - Generating periodic cash flow reports (e.g., daily, monthly, quarterly)
    /// - Auditing all cash movements across the organization within a specific period
    ///
    /// **Integration Pattern:**
    /// - Consumers typically call this endpoint when building aggregate cash flow views
    ///   that span multiple bank accounts.
    /// - When both date filters are omitted, all transactions are returned; apply date
    ///   filters for production usage to limit payload size.
    ///
    /// **Business Rules:**
    /// - Date range is inclusive on both ends (fromDate and toDate)
    /// - Results include transactions of all types: receipts, payments, and transfers
    /// - Transactions are returned regardless of reconciliation status
    ///
    /// **Authorization:** Requires Finance read permission.
    /// </remarks>
    /// <param name="fromDate">Optional start date to filter transactions (inclusive). Transactions on or after this date are included.</param>
    /// <param name="toDate">Optional end date to filter transactions (inclusive). Transactions on or before this date are included.</param>
    /// <returns>A list of cash transaction DTOs matching the specified criteria.</returns>
    /// <response code="200">Returns the list of cash transactions. May be empty if no transactions match the filters.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while retrieving transactions.</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CashTransactionDto>>> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var transactions = await _cashTransactionService.GetAllAsync(fromDate, toDate);
        return Ok(transactions);
    }

    /// <summary>
    /// Retrieves a single cash transaction by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Viewing full details of a specific cash transaction from a list or search result
    /// - Loading transaction data for editing, reversal, or audit review
    /// - Deep-linking to a transaction from bank reconciliation or journal entry screens
    ///
    /// **Integration Pattern:**
    /// - This endpoint is commonly invoked after a list endpoint returns summary data
    ///   and the user selects a specific record for detailed viewing.
    /// - The returned DTO includes all transaction metadata needed for display, including
    ///   the linked bank account, amount, and reconciliation status.
    ///
    /// **Business Rules:**
    /// - The transaction ID must correspond to an existing cash transaction record
    /// - Returns the full transaction DTO including line-level detail if applicable
    ///
    /// **Authorization:** Requires Finance read permission.
    /// </remarks>
    /// <param name="id">The unique identifier (GUID) of the cash transaction to retrieve.</param>
    /// <returns>The cash transaction DTO if found.</returns>
    /// <response code="200">Returns the requested cash transaction.</response>
    /// <response code="404">No cash transaction exists with the specified ID.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while retrieving the transaction.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<CashTransactionDto>> GetById(Guid id)
    {
        var transaction = await _cashTransactionService.GetByIdAsync(id);
        if (transaction == null)
            return NotFound();

        return Ok(transaction);
    }

    /// <summary>
    /// Retrieves all cash transactions for a specific bank account, optionally filtered by date range.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Displaying the transaction history (statement) for a particular bank account
    /// - Preparing data for bank reconciliation by viewing all activity on an account
    /// - Generating account-specific cash flow reports for management review
    ///
    /// **Integration Pattern:**
    /// - Typically called from the bank account detail view or bank reconciliation screen
    /// - The bank account ID is obtained from the Bank Account register endpoints
    /// - Results feed into the bank reconciliation workflow where transactions are matched
    ///   against imported bank statement lines
    ///
    /// **Business Rules:**
    /// - The bank account ID must reference a valid, existing bank account
    /// - Date range is inclusive on both ends when filters are provided
    /// - Returns all transaction types (receipts, payments, transfers) for the account
    /// - Both sides of a transfer (debit and credit) appear under their respective accounts
    ///
    /// **Authorization:** Requires Finance read permission.
    /// </remarks>
    /// <param name="bankAccountId">The unique identifier (GUID) of the bank account to query transactions for.</param>
    /// <param name="fromDate">Optional start date to filter transactions (inclusive).</param>
    /// <param name="toDate">Optional end date to filter transactions (inclusive).</param>
    /// <returns>A list of cash transaction DTOs for the specified bank account.</returns>
    /// <response code="200">Returns the list of cash transactions for the bank account. May be empty.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while retrieving transactions.</response>
    [HttpGet("bank-account/{bankAccountId}")]
    public async Task<ActionResult<IEnumerable<CashTransactionDto>>> GetByBankAccount(
        Guid bankAccountId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var transactions = await _cashTransactionService.GetByBankAccountAsync(bankAccountId, fromDate, toDate);
        return Ok(transactions);
    }

    /// <summary>
    /// Retrieves all unreconciled cash transactions for a specific bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Loading the list of outstanding (unmatched) transactions during bank reconciliation
    /// - Identifying cash transactions that have not yet been matched to bank statement lines
    /// - Monitoring the volume of unreconciled items to ensure timely reconciliation
    ///
    /// **Integration Pattern:**
    /// - This is the primary data source for the bank reconciliation matching screen
    /// - The bank reconciliation module calls this endpoint to present unreconciled items
    ///   alongside imported bank statement lines for manual or automatic matching
    /// - Once a transaction is reconciled, it no longer appears in subsequent calls
    ///
    /// **Business Rules:**
    /// - Only transactions with an unreconciled status are returned
    /// - The bank account ID must reference a valid, existing bank account
    /// - Includes all unreconciled transaction types: receipts, payments, and transfers
    /// - Transactions remain unreconciled until explicitly matched during the bank
    ///   reconciliation process
    ///
    /// **Authorization:** Requires Finance read permission.
    /// </remarks>
    /// <param name="bankAccountId">The unique identifier (GUID) of the bank account to retrieve unreconciled transactions for.</param>
    /// <returns>A list of unreconciled cash transaction DTOs for the specified bank account.</returns>
    /// <response code="200">Returns the list of unreconciled cash transactions. May be empty if all transactions are reconciled.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while retrieving unreconciled transactions.</response>
    [HttpGet("bank-account/{bankAccountId}/unreconciled")]
    public async Task<ActionResult<IEnumerable<CashTransactionDto>>> GetUnreconciled(Guid bankAccountId)
    {
        var transactions = await _cashTransactionService.GetUnreconciledAsync(bankAccountId);
        return Ok(transactions);
    }

    /// <summary>
    /// Creates a new cash receipt transaction recording money received into a bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Recording customer payments received via cheque, bank deposit, or electronic transfer
    /// - Logging miscellaneous income such as interest received or insurance refunds
    /// - Capturing cash sales proceeds deposited into the bank account
    ///
    /// **Integration Pattern:**
    /// - A corresponding journal entry is automatically generated: debit the bank account
    ///   (asset increases) and credit the revenue or receivable account.
    /// - When linked to a customer invoice, the Accounts Receivable module updates the
    ///   outstanding balance on the invoice accordingly.
    /// - The transaction is initially created with an unreconciled status for later matching
    ///   during bank reconciliation.
    ///
    /// **Business Rules:**
    /// - The target bank account must exist and be active
    /// - The receipt amount must be a positive value
    /// - A valid GL account must be specified for the offsetting credit entry
    /// - The transaction date must fall within an open fiscal period
    /// - A reference number or description should be provided for audit traceability
    ///
    /// **Authorization:** Requires Finance write permission.
    /// </remarks>
    /// <param name="dto">The cash receipt details including bank account, amount, date, and offsetting account information.</param>
    /// <returns>The newly created cash receipt transaction DTO with its assigned ID.</returns>
    /// <response code="201">Cash receipt created successfully. Returns the new transaction with a Location header pointing to the GetById endpoint.</response>
    /// <response code="400">Validation failed. The request body is invalid, the bank account does not exist, the amount is non-positive, or the fiscal period is closed.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while creating the receipt.</response>
    [HttpPost("receipt")]
    public async Task<ActionResult<CashTransactionDto>> CreateReceipt([FromBody] CreateCashReceiptDto dto)
    {
        try
        {
            var transaction = await _cashTransactionService.CreateReceiptAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new cash payment transaction recording money paid out from a bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Recording vendor/supplier payments via cheque, bank transfer, or direct debit
    /// - Logging operational expense payments such as rent, utilities, or payroll
    /// - Capturing petty cash replenishments or ad-hoc disbursements
    ///
    /// **Integration Pattern:**
    /// - A corresponding journal entry is automatically generated: credit the bank account
    ///   (asset decreases) and debit the expense or payable account.
    /// - When linked to a vendor invoice, the Accounts Payable module updates the
    ///   outstanding balance on the bill accordingly.
    /// - The transaction is initially created with an unreconciled status for later matching
    ///   during bank reconciliation.
    ///
    /// **Business Rules:**
    /// - The source bank account must exist and be active
    /// - The payment amount must be a positive value
    /// - A valid GL account must be specified for the offsetting debit entry
    /// - The transaction date must fall within an open fiscal period
    /// - Sufficient documentation (reference, payee, description) should accompany each payment
    ///
    /// **Authorization:** Requires Finance write permission.
    /// </remarks>
    /// <param name="dto">The cash payment details including bank account, amount, date, payee, and offsetting account information.</param>
    /// <returns>The newly created cash payment transaction DTO with its assigned ID.</returns>
    /// <response code="201">Cash payment created successfully. Returns the new transaction with a Location header pointing to the GetById endpoint.</response>
    /// <response code="400">Validation failed. The request body is invalid, the bank account does not exist, the amount is non-positive, or the fiscal period is closed.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while creating the payment.</response>
    [HttpPost("payment")]
    public async Task<ActionResult<CashTransactionDto>> CreatePayment([FromBody] CreateCashPaymentDto dto)
    {
        try
        {
            var transaction = await _cashTransactionService.CreatePaymentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates an inter-bank transfer moving funds from one bank account to another.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Transferring surplus funds from an operating account to a savings or investment account
    /// - Moving funds between currency accounts for foreign exchange requirements
    /// - Replenishing a secondary account (e.g., payroll account) from the main operating account
    ///
    /// **Integration Pattern:**
    /// - Two linked cash transactions are created: a payment (debit) on the source account
    ///   and a receipt (credit) on the destination account.
    /// - Corresponding journal entries are generated for both sides: credit the source bank
    ///   account and debit the destination bank account.
    /// - Both transactions are created with unreconciled status and must be individually
    ///   reconciled against their respective bank statements.
    ///
    /// **Business Rules:**
    /// - Both the source and destination bank accounts must exist and be active
    /// - The source and destination accounts must be different
    /// - The transfer amount must be a positive value
    /// - The transaction date must fall within an open fiscal period
    /// - If accounts are in different currencies, exchange rate handling may apply
    /// - The transfer creates a balanced pair: total debits equal total credits
    ///
    /// **Authorization:** Requires Finance write permission.
    /// </remarks>
    /// <param name="dto">The bank transfer details including source account, destination account, amount, date, and description.</param>
    /// <returns>An object containing both the source (fromTransaction) and destination (toTransaction) cash transaction DTOs.</returns>
    /// <response code="200">Bank transfer created successfully. Returns both the source and destination transaction records.</response>
    /// <response code="400">Validation failed. The accounts are invalid, identical, the amount is non-positive, or the fiscal period is closed.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="500">Internal server error occurred while creating the transfer.</response>
    [HttpPost("transfer")]
    public async Task<ActionResult<object>> CreateTransfer([FromBody] CreateBankTransferDto dto)
    {
        try
        {
            var (fromTransaction, toTransaction) = await _cashTransactionService.CreateTransferAsync(dto);
            return Ok(new { fromTransaction, toTransaction });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a cash transaction by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Removing an erroneously entered cash transaction before it is reconciled
    /// - Cleaning up duplicate transactions created by data entry mistakes
    /// - Voiding a transaction that was entered against the wrong bank account
    ///
    /// **Integration Pattern:**
    /// - Deleting a cash transaction should also reverse or remove any associated journal
    ///   entries to maintain general ledger integrity.
    /// - If the transaction was part of a bank transfer pair, consider whether the
    ///   corresponding contra-transaction also needs to be removed.
    /// - The bank reconciliation module should reflect the removal immediately in the
    ///   unreconciled items list.
    ///
    /// **Business Rules:**
    /// - The transaction must exist; otherwise a validation error is returned
    /// - Reconciled transactions typically cannot be deleted; they must be unreconciled first
    /// - Transactions in closed fiscal periods may be restricted from deletion
    /// - Deletion is permanent; consider using a reversal entry for audit-sensitive environments
    ///
    /// **Authorization:** Requires Finance delete permission.
    /// </remarks>
    /// <param name="id">The unique identifier (GUID) of the cash transaction to delete.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Cash transaction deleted successfully.</response>
    /// <response code="400">Deletion failed. The transaction may be reconciled, in a closed period, or otherwise restricted.</response>
    /// <response code="401">Not authenticated. A valid authentication token is required.</response>
    /// <response code="404">No cash transaction exists with the specified ID.</response>
    /// <response code="500">Internal server error occurred while deleting the transaction.</response>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _cashTransactionService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
