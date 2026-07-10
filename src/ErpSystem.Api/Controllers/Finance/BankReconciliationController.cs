using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Manages the bank reconciliation lifecycle, enabling organizations to match internal ledger transactions against external bank statement entries.
/// </summary>
/// <remarks>
/// **Domain Responsibility:**
/// Bank reconciliation is a critical financial control process that ensures the
/// general ledger bank account balances agree with the corresponding bank statement
/// balances. This controller orchestrates the full reconciliation workflow including
/// starting a reconciliation session, auto-matching and manual-matching of
/// transactions, and final approval.
///
/// **Key Workflow:**
/// 1. Start a reconciliation session for a specific bank account and statement period
/// 2. Run auto-match to pair bank statement lines with ledger transactions by amount, date, and reference
/// 3. Manually match any remaining unmatched items
/// 4. Review the reconciliation summary to verify the balance difference is zero
/// 5. Approve the reconciliation to finalize and lock the period
///
/// **Integration Points:**
/// - **Bank Accounts:** Reconciliations are always scoped to a single bank account
/// - **General Ledger:** Matched transactions tie back to journal entries posted against the bank GL account
/// - **Cash Management:** Completed reconciliations feed into cash position reporting
/// - **Audit Trail:** Each reconciliation and its matches are persisted for audit and compliance purposes
/// </remarks>
[ApiController]
[Authorize]
[Route("api/finance/bank-reconciliation")]
public class BankReconciliationController : ControllerBase
{
    private readonly IBankReconciliationService _reconciliationService;

    public BankReconciliationController(IBankReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    /// <summary>
    /// Retrieves a single bank reconciliation session by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Loading a previously started reconciliation to continue matching transactions
    /// - Reviewing the current state of a reconciliation before approval
    /// - Fetching reconciliation details for audit or reporting purposes
    ///
    /// **Integration Pattern:**
    /// - Typically called after StartReconciliation to reload the session
    /// - Used by the UI to display the reconciliation workspace with matched and unmatched items
    ///
    /// **Business Rules:**
    /// - Returns the full reconciliation record regardless of its status (Draft, InProgress, Approved)
    /// - Includes associated bank account and period information
    /// </remarks>
    /// <param name="id">The unique identifier of the bank reconciliation session.</param>
    /// <returns>The bank reconciliation details including status, bank account, and period information.</returns>
    /// <response code="200">The reconciliation was found and returned successfully.</response>
    /// <response code="404">No reconciliation exists with the specified identifier.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<BankReconciliationDto>> GetById(Guid id)
    {
        var reconciliation = await _reconciliationService.GetByIdAsync(id);
        if (reconciliation == null)
            return NotFound();

        return Ok(reconciliation);
    }

    /// <summary>
    /// Retrieves all bank reconciliation sessions for a specific bank account.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Displaying the reconciliation history for a bank account
    /// - Checking which periods have already been reconciled before starting a new session
    /// - Auditing the reconciliation trail for a particular bank account over time
    ///
    /// **Integration Pattern:**
    /// - Called from the bank account detail view to show reconciliation history
    /// - The bank account ID corresponds to a record managed by the Bank Accounts module
    ///
    /// **Business Rules:**
    /// - Returns reconciliations in all statuses (Draft, InProgress, Approved)
    /// - Results are scoped to the specified bank account only
    /// - An empty collection is returned if no reconciliations exist for the account
    /// </remarks>
    /// <param name="bankAccountId">The unique identifier of the bank account to retrieve reconciliations for.</param>
    /// <returns>A collection of bank reconciliation sessions associated with the specified bank account.</returns>
    /// <response code="200">The list of reconciliations was retrieved successfully (may be empty).</response>
    [HttpGet("bank-account/{bankAccountId}")]
    public async Task<ActionResult<IEnumerable<BankReconciliationDto>>> GetByBankAccount(Guid bankAccountId)
    {
        var reconciliations = await _reconciliationService.GetByBankAccountAsync(bankAccountId);
        return Ok(reconciliations);
    }

    /// <summary>
    /// Initiates a new bank reconciliation session for a given bank account and statement period.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Beginning the monthly bank reconciliation process after receiving the bank statement
    /// - Starting an ad-hoc reconciliation to investigate discrepancies
    /// - Creating a reconciliation session to import and match bank statement lines
    ///
    /// **Integration Pattern:**
    /// - This is the entry point of the reconciliation workflow; must be called before auto-match or manual-match
    /// - The created reconciliation ID is used in all subsequent matching and approval operations
    /// - Loads unmatched ledger transactions for the bank account within the statement period
    ///
    /// **Business Rules:**
    /// - A bank account may not have overlapping reconciliation periods
    /// - The statement ending balance must be provided to compute the reconciliation difference
    /// - The reconciliation is created in Draft status and must be explicitly approved to finalize
    /// - Only one active (non-approved) reconciliation per bank account is typically allowed
    ///
    /// **Authorization:** Requires finance or bank reconciliation permission.
    /// </remarks>
    /// <param name="dto">The reconciliation start parameters including bank account ID, statement date, and ending balance.</param>
    /// <returns>The newly created bank reconciliation session with its assigned identifier.</returns>
    /// <response code="201">The reconciliation session was created successfully.</response>
    /// <response code="400">Validation failed (e.g., overlapping period, missing required fields, or invalid bank account).</response>
    [HttpPost("start")]
    public async Task<ActionResult<BankReconciliationDto>> StartReconciliation([FromBody] StartReconciliationDto dto)
    {
        try
        {
            var reconciliation = await _reconciliationService.StartReconciliationAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = reconciliation.Id }, reconciliation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Runs the automatic matching algorithm to pair bank statement lines with ledger transactions.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Automatically matching the majority of transactions after starting a reconciliation
    /// - Re-running auto-match after importing additional bank statement lines
    /// - Reducing manual effort by letting the system identify clear matches by amount, date, and reference
    ///
    /// **Integration Pattern:**
    /// - Should be called after StartReconciliation and before manual matching
    /// - Returns the set of newly created matches so the UI can highlight them for review
    /// - Unmatched items after auto-match require manual intervention via the manual-match endpoint
    ///
    /// **Business Rules:**
    /// - Matches are determined by comparing transaction amounts, dates, and reference numbers
    /// - Only unmatched statement lines and ledger transactions are considered
    /// - The reconciliation must be in Draft or InProgress status; approved reconciliations cannot be auto-matched
    /// - Auto-match does not finalize the reconciliation; approval is still required
    /// - Previously matched items are not re-evaluated
    /// </remarks>
    /// <param name="id">The unique identifier of the bank reconciliation session to auto-match.</param>
    /// <returns>A collection of newly created matches produced by the auto-match algorithm.</returns>
    /// <response code="200">Auto-matching completed successfully; returns the list of new matches (may be empty if no matches found).</response>
    /// <response code="400">The reconciliation is in an invalid state for auto-matching (e.g., already approved) or does not exist.</response>
    [HttpPost("{id}/auto-match")]
    public async Task<ActionResult<IEnumerable<ReconciliationMatchDto>>> AutoMatch(Guid id)
    {
        try
        {
            var matches = await _reconciliationService.AutoMatchAsync(id);
            return Ok(matches);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a manual match between a bank statement line and one or more ledger transactions.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Matching transactions that auto-match could not pair due to differing references or split amounts
    /// - Pairing a single bank deposit with multiple ledger receipt entries (many-to-one matching)
    /// - Resolving discrepancies where the bank and ledger record the same transaction with different details
    ///
    /// **Integration Pattern:**
    /// - Typically called after auto-match to handle remaining unmatched items
    /// - The reconciliation ID in the route must match the session being worked on
    /// - The route parameter overrides any reconciliation ID provided in the request body to ensure consistency
    ///
    /// **Business Rules:**
    /// - The reconciliation must be in Draft or InProgress status
    /// - A statement line or ledger transaction that is already matched cannot be matched again
    /// - The matched amounts should ideally balance, though partial matches may be allowed depending on configuration
    /// - Manual matches are recorded with an audit trail identifying the user who created them
    ///
    /// **Authorization:** Requires finance or bank reconciliation permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the bank reconciliation session.</param>
    /// <param name="dto">The manual match details including the statement line ID and ledger transaction IDs to pair.</param>
    /// <returns>The newly created match record with its assigned identifier and matched item details.</returns>
    /// <response code="200">The manual match was created successfully.</response>
    /// <response code="400">Validation failed (e.g., items already matched, reconciliation not in valid state, or mismatched amounts).</response>
    [HttpPost("{id}/manual-match")]
    public async Task<ActionResult<ReconciliationMatchDto>> CreateManualMatch(
        Guid id,
        [FromBody] CreateManualMatchDto dto)
    {
        try
        {
            // Ensure reconciliation ID matches
            dto.ReconciliationId = id;
            var match = await _reconciliationService.CreateManualMatchAsync(dto);
            return Ok(match);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a previously created match from the reconciliation.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Undoing an incorrect auto-match that paired the wrong transactions
    /// - Removing a manual match to re-pair the items differently
    /// - Cleaning up matches before re-running the auto-match algorithm
    ///
    /// **Integration Pattern:**
    /// - After removal, the previously matched statement line and ledger transactions become available for re-matching
    /// - The reconciliation summary should be refreshed after removing matches to reflect the updated balance
    ///
    /// **Business Rules:**
    /// - Matches can only be removed from reconciliations in Draft or InProgress status
    /// - Matches on approved reconciliations cannot be removed; the approval must be reversed first
    /// - Removing a match restores the associated statement lines and ledger transactions to unmatched status
    /// - The operation is idempotent from an audit perspective; the removal is recorded in the audit trail
    /// </remarks>
    /// <param name="matchId">The unique identifier of the reconciliation match to remove.</param>
    /// <returns>The removed match record for confirmation purposes.</returns>
    /// <response code="200">The match was removed successfully.</response>
    /// <response code="400">The match could not be removed (e.g., reconciliation is already approved or match does not exist).</response>
    [HttpDelete("match/{matchId}")]
    public async Task<ActionResult<ReconciliationMatchDto>> RemoveMatch(Guid matchId)
    {
        try
        {
            var match = await _reconciliationService.RemoveMatchAsync(matchId);
            return Ok(match);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates and posts an explicit reconciliation adjustment through the central Finance posting engine.
    /// </summary>
    [HttpPost("{id}/adjustments/post")]
    public async Task<ActionResult<ReconciliationAdjustmentDto>> CreateAndPostAdjustment(
        Guid id,
        [FromBody] CreateReconciliationAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var adjustment = await _reconciliationService.CreateAndPostAdjustmentAsync(id, dto, cancellationToken);
            return Ok(adjustment);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Finalizes a reconciliation after posted GL book balance agrees to the statement balance.
    /// </summary>
    [HttpPost("{id}/finalize")]
    public async Task<ActionResult<BankReconciliationDto>> Finalize(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var reconciliation = await _reconciliationService.FinalizeReconciliationAsync(id, cancellationToken);
            return Ok(reconciliation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels an open reconciliation that has not been finalized.
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<BankReconciliationDto>> Cancel(
        Guid id,
        [FromBody] CancelReconciliationDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var reconciliation = await _reconciliationService.CancelReconciliationAsync(id, dto.Reason, cancellationToken);
            return Ok(reconciliation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Approves and finalizes a bank reconciliation session, locking it from further changes.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Finalizing the monthly bank reconciliation after all items are matched and the balance difference is zero
    /// - Approving a reconciliation as part of the month-end close process
    /// - Signing off on the reconciliation for audit and compliance purposes
    ///
    /// **Integration Pattern:**
    /// - This is the final step in the reconciliation workflow after all matching is complete
    /// - Once approved, the reconciliation period is considered closed for the bank account
    /// - Downstream reporting (cash position, bank balance confirmations) relies on approved reconciliations
    /// - The approval timestamp and user are recorded for audit trail purposes
    ///
    /// **Business Rules:**
    /// - The reconciliation difference (statement balance minus ledger balance minus unmatched items) should be zero before approval
    /// - Once approved, no further matches can be added or removed
    /// - Approval is irreversible through this endpoint; a separate reversal process may be required to reopen
    /// - Only users with appropriate authorization may approve reconciliations
    ///
    /// **Authorization:** Requires finance approval or bank reconciliation approval permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the bank reconciliation session to approve.</param>
    /// <returns>The updated bank reconciliation record with approved status and approval metadata.</returns>
    /// <response code="200">The reconciliation was approved and finalized successfully.</response>
    /// <response code="400">Approval failed (e.g., outstanding unmatched items, non-zero difference, or reconciliation not in valid state).</response>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<BankReconciliationDto>> Approve(Guid id)
    {
        try
        {
            var reconciliation = await _reconciliationService.ApproveReconciliationAsync(id);
            return Ok(reconciliation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a summary of the current reconciliation state including balance comparisons and match statistics.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Displaying a dashboard view of the reconciliation progress (matched vs. unmatched counts and amounts)
    /// - Verifying the reconciliation difference is zero before proceeding to approval
    /// - Generating a reconciliation summary report for management review
    ///
    /// **Integration Pattern:**
    /// - Typically called after each matching operation to refresh the reconciliation progress display
    /// - The summary provides the data needed for the reconciliation balance worksheet
    /// - Used by the approval workflow to validate readiness before allowing the approve action
    ///
    /// **Business Rules:**
    /// - The summary includes the statement ending balance, book balance, and the computed difference
    /// - Match statistics include counts and totals for matched, unmatched statement lines, and unmatched ledger transactions
    /// - The summary is computed in real-time based on the current state of matches
    /// - Available for reconciliations in any status (Draft, InProgress, or Approved)
    /// </remarks>
    /// <param name="id">The unique identifier of the bank reconciliation session to summarize.</param>
    /// <returns>The reconciliation summary including balances, difference, and match statistics.</returns>
    /// <response code="200">The reconciliation summary was computed and returned successfully.</response>
    /// <response code="404">No reconciliation exists with the specified identifier.</response>
    [HttpGet("{id}/summary")]
    public async Task<ActionResult<ReconciliationSummaryDto>> GetSummary(Guid id)
    {
        try
        {
            var summary = await _reconciliationService.GetSummaryAsync(id);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
