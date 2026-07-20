using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Manages organizational budgeting workflows including scenarios, budget returns (assignments), and line-item budget entries.
/// </summary>
/// <remarks>
/// **Domain Responsibility:**
/// This controller orchestrates the full budget lifecycle: creating planning scenarios tied to
/// fiscal years, assigning budget returns to departments or cost centres, capturing line-item
/// entries against GL accounts, and driving the submission/approval workflow that culminates
/// in a locked, board-approved budget.
///
/// **Key Concepts:**
/// - **Scenario** -- A named budget version (e.g., "Original", "Mid-Year Revision") scoped to a fiscal year.
///   Multiple scenarios allow what-if analysis before one is locked as the official budget.
/// - **Return** -- A budget assignment that a department or cost centre must complete and submit.
///   Returns move through Draft -> Submitted -> Approved/Rejected states.
/// - **Entry** -- An individual line-item amount for a specific GL account and period within a return.
///
/// **Integration Patterns:**
/// - Fiscal year IDs originate from the Fiscal Year / Period module.
/// - GL account references align with the Chart of Accounts module.
/// - Approved budgets feed into Budget-vs-Actual variance reporting and commitment control.
///
/// **Authorization:** All endpoints require authentication and the action-specific finance
/// budgeting permission applied by <see cref="ErpSystem.Api.Authorization.FinancePermissionAuthorizationConvention"/>.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUserService _currentUserService;

    public BudgetController(IBudgetService budgetService, ICurrentUserService currentUserService)
    {
        _budgetService = budgetService;
        _currentUserService = currentUserService;
    }

    // ========================================================================
    // SCENARIOS
    // ========================================================================

    /// <summary>
    /// Retrieves all budget scenarios associated with a given fiscal year.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Populating a scenario selector dropdown when opening the budget module for a fiscal year.
    /// - Comparing multiple budget versions (e.g., "Original" vs. "Revised") side by side.
    ///
    /// **Integration Pattern:**
    /// - The fiscal year ID is obtained from the Fiscal Year / Period module endpoints.
    /// - The returned scenarios feed into the budget returns and entries sub-workflows.
    ///
    /// **Business Rules:**
    /// - Returns both locked and unlocked scenarios; the caller should inspect the IsLocked flag
    ///   to determine editability.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="fiscalYearId">The unique identifier of the fiscal year whose scenarios are requested.</param>
    /// <returns>A collection of <see cref="BudgetScenarioDto"/> for the specified fiscal year.</returns>
    /// <response code="200">Budget scenarios returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    [HttpGet("scenarios/year/{fiscalYearId}")]
    public async Task<ActionResult<IEnumerable<BudgetScenarioDto>>> GetScenariosForYear(Guid fiscalYearId)
    {
        var result = await _budgetService.GetScenariosForYearAsync(fiscalYearId);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single budget scenario by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Loading full scenario details (name, description, lock status) for display or editing.
    /// - Refreshing scenario state after a lock or update operation.
    ///
    /// **Integration Pattern:**
    /// - Typically called after selecting a scenario from the list returned by
    ///   <c>GET scenarios/year/{fiscalYearId}</c>.
    ///
    /// **Business Rules:**
    /// - Returns 404 if the scenario does not exist or has been deleted.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the budget scenario.</param>
    /// <returns>The matching <see cref="BudgetScenarioDto"/>.</returns>
    /// <response code="200">Budget scenario returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Scenario with the specified ID was not found.</response>
    [HttpGet("scenarios/{id}")]
    public async Task<ActionResult<BudgetScenarioDto>> GetScenario(Guid id)
    {
        try
        {
            var result = await _budgetService.GetScenarioAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Creates a new budget scenario for a fiscal year.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Starting a fresh budget planning cycle (e.g., creating the "FY2026 Original Budget" scenario).
    /// - Creating an alternative scenario for what-if analysis before final approval.
    ///
    /// **Integration Pattern:**
    /// - The DTO must reference a valid fiscal year ID from the Fiscal Year module.
    /// - After creation, budget returns and entries can be added under this scenario.
    ///
    /// **Business Rules:**
    /// - A new scenario is created in an unlocked state, allowing returns and entries to be added.
    /// - Scenario names should be unique within a fiscal year for clarity, though this may be
    ///   enforced at the service layer.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="dto">The details for the new budget scenario including name, description, and fiscal year reference.</param>
    /// <returns>The newly created <see cref="BudgetScenarioDto"/> with its assigned ID.</returns>
    /// <response code="201">Scenario created successfully. Location header points to the new resource.</response>
    /// <response code="400">Validation failure in the supplied DTO.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    [HttpPost("scenarios")]
    public async Task<ActionResult<BudgetScenarioDto>> CreateScenario(CreateBudgetScenarioDto dto)
    {
        var result = await _budgetService.CreateScenarioAsync(dto);
        return CreatedAtAction(nameof(GetScenario), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing budget scenario's metadata (e.g., name, description).
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Renaming a scenario to better reflect its purpose (e.g., "Draft" to "Board-Approved").
    /// - Updating the description or notes attached to a scenario.
    ///
    /// **Integration Pattern:**
    /// - The route ID must match <c>dto.Id</c>; a mismatch returns 400 to prevent accidental overwrites.
    ///
    /// **Business Rules:**
    /// - A locked scenario cannot be updated; the service layer will throw an
    ///   <see cref="InvalidOperationException"/> resulting in a 400 response.
    /// - The route ID and DTO ID must match.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the scenario to update (must match <c>dto.Id</c>).</param>
    /// <param name="dto">The updated scenario details.</param>
    /// <returns>The updated <see cref="BudgetScenarioDto"/>.</returns>
    /// <response code="200">Scenario updated successfully.</response>
    /// <response code="400">Route ID / DTO ID mismatch, validation failure, or scenario is locked.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Scenario with the specified ID was not found.</response>
    [HttpPut("scenarios/{id}")]
    public async Task<ActionResult<BudgetScenarioDto>> UpdateScenario(Guid id, UpdateBudgetScenarioDto dto)
    {
        if (id != dto.Id) return BadRequest();
        try
        {
            var result = await _budgetService.UpdateScenarioAsync(dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a budget scenario and all associated returns and entries.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Removing a draft or superseded scenario that is no longer needed.
    /// - Cleaning up test or exploratory scenarios before final budget approval.
    ///
    /// **Integration Pattern:**
    /// - Cascading delete removes all child returns and entries; callers should confirm with the
    ///   user before invoking.
    ///
    /// **Business Rules:**
    /// - A locked scenario cannot be deleted; the service layer will reject the operation with
    ///   an <see cref="InvalidOperationException"/> resulting in a 400 response.
    /// - Returns 404 if the scenario does not exist.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the scenario to delete.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Scenario deleted successfully.</response>
    /// <response code="400">Scenario is locked and cannot be deleted.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Scenario with the specified ID was not found.</response>
    [HttpDelete("scenarios/{id}")]
    public async Task<ActionResult> DeleteScenario(Guid id)
    {
        try
        {
            var success = await _budgetService.DeleteScenarioAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Locks a budget scenario, preventing any further modifications to its returns and entries.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Finalizing the approved budget after board or management sign-off.
    /// - Freezing a scenario to preserve a point-in-time snapshot for variance reporting.
    ///
    /// **Integration Pattern:**
    /// - Once locked, the scenario's budget figures become the baseline for Budget-vs-Actual
    ///   variance analysis and commitment control checks.
    /// - Future permission checks (e.g., "Budget.Lock") may restrict this action to Finance Admins.
    ///
    /// **Business Rules:**
    /// - Locking is a one-way operation; a locked scenario cannot be unlocked through this endpoint.
    /// - All child returns should ideally be in an approved state before locking, though enforcement
    ///   depends on service-layer rules.
    ///
    /// **Authorization:** Requires the mapped finance budget-lock permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the scenario to lock.</param>
    /// <returns>The updated <see cref="BudgetScenarioDto"/> reflecting the locked state.</returns>
    /// <response code="200">Scenario locked successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Scenario with the specified ID was not found.</response>
    [HttpPost("scenarios/{id}/lock")]
    public async Task<ActionResult<BudgetScenarioDto>> LockScenario(Guid id)
    {
        try
        {
            // Validate user permissions (e.g., only Finance Admin)
            // if (!_currentUserService.HasPermission("Budget.Lock")) return Forbid();

            var result = await _budgetService.LockScenarioAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ========================================================================
    // RETURNS (ASSIGNMENTS)
    // ========================================================================

    /// <summary>
    /// Retrieves all budget returns (assignments) for a given scenario.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Listing all departmental budget submissions under a specific scenario for review.
    /// - Building a dashboard showing return completion and approval status across the organization.
    ///
    /// **Integration Pattern:**
    /// - Returns reference departments or cost centres from the organizational structure module.
    /// - Each return contains status information (Draft, Submitted, Approved, Rejected) for
    ///   workflow tracking.
    ///
    /// **Business Rules:**
    /// - Returns all budget returns regardless of status; the caller can filter by status client-side.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="scenarioId">The unique identifier of the parent budget scenario.</param>
    /// <returns>A collection of <see cref="BudgetReturnDto"/> for the specified scenario.</returns>
    /// <response code="200">Budget returns retrieved successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    [HttpGet("scenarios/{scenarioId}/returns")]
    public async Task<ActionResult<IEnumerable<BudgetReturnDto>>> GetReturns(Guid scenarioId)
    {
        var result = await _budgetService.GetReturnsForScenarioAsync(scenarioId);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single budget return by its unique identifier.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Loading the full details of a budget return for editing or review.
    /// - Refreshing return state after a submit, approve, or reject operation.
    ///
    /// **Integration Pattern:**
    /// - Typically followed by <c>GET returns/{returnId}/entries</c> to load the line-item details.
    ///
    /// **Business Rules:**
    /// - Returns 404 if the budget return does not exist.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the budget return.</param>
    /// <returns>The matching <see cref="BudgetReturnDto"/>.</returns>
    /// <response code="200">Budget return retrieved successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Budget return with the specified ID was not found.</response>
    [HttpGet("returns/{id}")]
    public async Task<ActionResult<BudgetReturnDto>> GetReturn(Guid id)
    {
        try
        {
            var result = await _budgetService.GetReturnAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Creates a new budget return (assignment) under a scenario.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Assigning a budget template to a department or cost centre for completion.
    /// - Initiating the budget collection process for a new organizational unit.
    ///
    /// **Integration Pattern:**
    /// - The DTO references a parent scenario and typically a department or cost centre.
    /// - After creation, line-item entries are added via the <c>POST entries/bulk-save</c> endpoint.
    ///
    /// **Business Rules:**
    /// - A new return is created in Draft status, allowing entries to be added and modified.
    /// - The parent scenario must be unlocked; creating returns under a locked scenario will fail.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="dto">The details for the new budget return including scenario reference and assignee information.</param>
    /// <returns>The newly created <see cref="BudgetReturnDto"/> with its assigned ID.</returns>
    /// <response code="201">Budget return created successfully. Location header points to the new resource.</response>
    /// <response code="400">Validation failure in the supplied DTO.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    [HttpPost("returns")]
    public async Task<ActionResult<BudgetReturnDto>> CreateReturn(CreateBudgetReturnDto dto)
    {
        try
        {
            var result = await _budgetService.CreateReturnAsync(dto);
            return CreatedAtAction(nameof(GetReturn), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Updates assignment metadata (assignee, approver, notes) on a Draft or Rejected budget return.
    /// </summary>
    /// <remarks>
    /// Amounts change through the entries bulk-save endpoint; status changes through the
    /// submit/approve/reject workflow endpoints.
    /// </remarks>
    /// <response code="200">Budget return updated successfully.</response>
    /// <response code="400">The return has already been submitted or approved.</response>
    /// <response code="404">Return with the specified ID was not found.</response>
    [HttpPut("returns/{id}")]
    public async Task<ActionResult<BudgetReturnDto>> UpdateReturn(Guid id, UpdateBudgetReturnDto dto)
    {
        try
        {
            var result = await _budgetService.UpdateReturnAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Submits a budget return for approval, transitioning it from Draft to Submitted status.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - A department head finalizing their budget figures and sending them for central review.
    /// - Triggering the approval workflow after all line-item entries have been captured.
    ///
    /// **Integration Pattern:**
    /// - After submission, the return appears in the approver's queue and can be approved or
    ///   rejected via the corresponding endpoints.
    /// - Notifications may be triggered to alert approvers of the pending submission.
    ///
    /// **Business Rules:**
    /// - Only returns in Draft status can be submitted; submitting an already-submitted or
    ///   approved return results in a 400 response.
    /// - The return should contain at least one entry before submission (enforcement may vary).
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the budget return to submit.</param>
    /// <returns>The updated <see cref="BudgetReturnDto"/> reflecting the Submitted status.</returns>
    /// <response code="200">Budget return submitted successfully.</response>
    /// <response code="400">Return is not in a submittable state (e.g., already submitted or approved).</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Budget return with the specified ID was not found.</response>
    [HttpPost("returns/{id}/submit")]
    public async Task<ActionResult<BudgetReturnDto>> SubmitReturn(Guid id)
    {
        try
        {
            var result = await _budgetService.SubmitReturnAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Approves a submitted budget return, transitioning it to Approved status.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - A finance manager or budget committee approving a department's budget submission.
    /// - Completing the approval step so the scenario can subsequently be locked.
    ///
    /// **Integration Pattern:**
    /// - The approving user's identity is captured from <see cref="ICurrentUserService"/> and
    ///   recorded against the return for audit purposes.
    /// - Once all returns in a scenario are approved, the scenario can be locked.
    ///
    /// **Business Rules:**
    /// - Only returns in Submitted status can be approved; approving a Draft or already-approved
    ///   return results in a 400 response.
    /// - The approver's user ID is recorded for audit trail compliance.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the budget return to approve.</param>
    /// <returns>The updated <see cref="BudgetReturnDto"/> reflecting the Approved status.</returns>
    /// <response code="200">Budget return approved successfully.</response>
    /// <response code="400">Return is not in a state that can be approved (e.g., still in Draft or already approved).</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Budget return with the specified ID was not found.</response>
    [HttpPost("returns/{id}/approve")]
    public async Task<ActionResult<BudgetReturnDto>> ApproveReturn(Guid id)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : Guid.Empty;
            var result = await _budgetService.ApproveReturnAsync(id, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Rejects a submitted budget return, transitioning it back so corrections can be made.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - A finance manager sending a budget submission back to the department with feedback.
    /// - Flagging specific issues (over-allocation, missing line items) via the rejection reason.
    ///
    /// **Integration Pattern:**
    /// - The rejecting user's identity is captured from <see cref="ICurrentUserService"/> for
    ///   audit purposes.
    /// - After rejection, the department can revise entries and re-submit the return.
    ///
    /// **Business Rules:**
    /// - Only returns in Submitted status can be rejected; rejecting a Draft or already-approved
    ///   return results in a 400 response.
    /// - A rejection reason is required and stored for audit and communication purposes.
    /// - The rejector's user ID is recorded for audit trail compliance.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="id">The unique identifier of the budget return to reject.</param>
    /// <param name="reason">A free-text explanation of why the return is being rejected.</param>
    /// <returns>The updated <see cref="BudgetReturnDto"/> reflecting the Rejected status.</returns>
    /// <response code="200">Budget return rejected successfully.</response>
    /// <response code="400">Return is not in a rejectable state or reason is missing.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">Budget return with the specified ID was not found.</response>
    [HttpPost("returns/{id}/reject")]
    public async Task<ActionResult<BudgetReturnDto>> RejectReturn(Guid id, [FromBody] string reason)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : Guid.Empty;
            var result = await _budgetService.RejectReturnAsync(id, reason, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ========================================================================
    // ENTRIES
    // ========================================================================

    /// <summary>
    /// Retrieves all budget entries (line items) for a given budget return.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Loading the budget grid/spreadsheet for a department to fill in or review.
    /// - Exporting budget line items for reporting or comparison with actuals.
    ///
    /// **Integration Pattern:**
    /// - Each entry references a GL account from the Chart of Accounts module and a period
    ///   from the Fiscal Year module.
    /// - Entries are typically displayed in a matrix of accounts (rows) by periods (columns).
    ///
    /// **Business Rules:**
    /// - All entries for the return are returned regardless of whether the return is in Draft,
    ///   Submitted, or Approved status.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="returnId">The unique identifier of the parent budget return.</param>
    /// <returns>A collection of <see cref="BudgetEntryDto"/> for the specified return.</returns>
    /// <response code="200">Budget entries retrieved successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    [HttpGet("returns/{returnId}/entries")]
    public async Task<ActionResult<IEnumerable<BudgetEntryDto>>> GetEntries(Guid returnId)
    {
        var result = await _budgetService.GetEntriesAsync(returnId);
        return Ok(result);
    }

    /// <summary>
    /// Saves multiple budget entries in a single bulk operation (create, update, or delete).
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Persisting an entire budget spreadsheet after the user clicks "Save" in the UI.
    /// - Importing budget figures from an external source (e.g., Excel upload) in one batch.
    ///
    /// **Integration Pattern:**
    /// - The DTO contains the parent return ID and a collection of entry records to upsert.
    /// - This replaces individual create/update/delete calls, reducing round-trips and ensuring
    ///   atomicity of the save operation.
    ///
    /// **Business Rules:**
    /// - The parent budget return must exist and must not be in a locked or approved state;
    ///   attempting to save entries against a submitted or approved return results in a 400 response.
    /// - Entry amounts must be non-negative where applicable (enforcement depends on service rules).
    /// - The operation is atomic: either all entries are saved or none are.
    ///
    /// **Authorization:** Requires the mapped finance budgeting permission.
    /// </remarks>
    /// <param name="dto">The bulk save payload containing the return ID and collection of budget entries to persist.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Entries saved successfully.</response>
    /// <response code="400">Validation failure, return is in a non-editable state, or business rule violation.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User lacks the required finance budgeting permission.</response>
    /// <response code="404">The parent budget return was not found.</response>
    [HttpPost("entries/bulk-save")]
    public async Task<ActionResult> BulkSaveEntries(BulkSaveBudgetEntriesDto dto)
    {
        try
        {
            await _budgetService.BulkSaveEntriesAsync(dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ========================================================================
    // ANALYTICS
    // ========================================================================

    /// <summary>
    /// Base-currency revenue/expense totals for a scenario, aggregated from approved returns.
    /// </summary>
    /// <response code="200">Scenario summary returned successfully.</response>
    /// <response code="404">Scenario with the specified ID was not found.</response>
    [HttpGet("analytics/summary/{scenarioId}")]
    public async Task<ActionResult<BudgetSummaryDto>> GetScenarioSummary(Guid scenarioId)
    {
        try
        {
            var result = await _budgetService.GetScenarioSummaryAsync(scenarioId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
