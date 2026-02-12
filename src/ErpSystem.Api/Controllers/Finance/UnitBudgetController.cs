using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// API controller for Unit Account Budget operations.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/finance/unit-budgets")]
    public class UnitBudgetController : ControllerBase
    {
        private readonly IUnitBudgetService _budgetService;

        public UnitBudgetController(IUnitBudgetService budgetService)
        {
            _budgetService = budgetService;
        }

        /// <summary>
        /// Gets all unit account budgets.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the full list of budgets for display in a budget overview grid
        /// - Exporting all budget data for external reporting or analysis
        /// - Populating dropdowns or selection lists that require all budget entries
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint on page load for the budget management screen
        /// - Combine with fiscal period lookups to display period-aware budget views
        /// - Use alongside the variances endpoint to build comprehensive budget dashboards
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive budget entries
        /// - Results include resolved account names, period names, and unit type codes
        /// - No pagination is applied; all records are returned
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all unit account budgets</returns>
        /// <response code="200">Returns the list of unit account budgets</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetAll()
        {
            var budgets = await _budgetService.GetAllAsync();
            return Ok(budgets);
        }

        /// <summary>
        /// Gets a specific budget by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Retrieving a single budget entry for viewing or editing in a detail form
        /// - Fetching budget details after a create or update operation for confirmation
        /// - Loading budget data for a drill-down view from a summary list
        ///
        /// **Integration Pattern:**
        /// - Use the ID returned from the Create endpoint to fetch the newly created budget
        /// - Call before presenting an edit form to populate current values
        /// - Returns 404 if the budget ID does not exist
        ///
        /// **Business Rules:**
        /// - Returns the full budget record including resolved account and period names
        /// - Inactive budgets are still retrievable by ID
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the budget entry</param>
        /// <returns>The unit account budget matching the specified ID</returns>
        /// <response code="200">Returns the requested budget</response>
        /// <response code="404">Budget with the specified ID was not found</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<UnitAccountBudgetDto>> GetById(Guid id)
        {
            var budget = await _budgetService.GetByIdAsync(id);
            if (budget == null)
                return NotFound(new { error = $"Budget with ID '{id}' not found" });
            return Ok(budget);
        }

        /// <summary>
        /// Gets all budgets for a specific unit account.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing the budget history for a particular unit account across all periods
        /// - Analyzing budget trends for a specific account over multiple fiscal years
        /// - Building account-level budget reports and comparisons
        ///
        /// **Integration Pattern:**
        /// - Use the unit account ID from the Chart of Accounts or Unit Accounts endpoints
        /// - Combine with period data to create timeline visualizations of account budgets
        /// - Returns an empty list if no budgets exist for the specified account
        ///
        /// **Business Rules:**
        /// - Returns all budget entries (active and inactive) for the given account
        /// - Multiple budget versions for the same account and period may be returned
        /// - Results are not filtered by fiscal year; all periods are included
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="accountId">The unique identifier of the unit account</param>
        /// <returns>List of budgets associated with the specified unit account</returns>
        /// <response code="200">Returns the list of budgets for the account</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("by-account/{accountId}")]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetByAccount(Guid accountId)
        {
            var budgets = await _budgetService.GetByAccountAsync(accountId);
            return Ok(budgets);
        }

        /// <summary>
        /// Gets all budgets for a specific fiscal period.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing all account budgets allocated to a particular fiscal period (e.g., January 2026)
        /// - Generating period-level budget summary reports
        /// - Preparing budget review meetings for a specific month or quarter
        ///
        /// **Integration Pattern:**
        /// - Use the fiscal period ID from the Fiscal Period endpoints
        /// - Combine with the variances endpoint to compare budgets against actuals for the same period
        /// - Returns an empty list if no budgets exist for the specified period
        ///
        /// **Business Rules:**
        /// - Returns all budget entries (active and inactive) for the given fiscal period
        /// - Includes budgets across all unit accounts for the specified period
        /// - Multiple budget versions for the same period may be returned
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="periodId">The unique identifier of the fiscal period</param>
        /// <returns>List of budgets associated with the specified fiscal period</returns>
        /// <response code="200">Returns the list of budgets for the period</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("by-period/{periodId}")]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetByPeriod(Guid periodId)
        {
            var budgets = await _budgetService.GetByPeriodAsync(periodId);
            return Ok(budgets);
        }

        /// <summary>
        /// Gets budget vs actual variances.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Generating budget-to-actual variance reports for management review
        /// - Identifying accounts that are over or under budget
        /// - Building variance dashboards with favorable/unfavorable indicators
        ///
        /// **Integration Pattern:**
        /// - Call without periodId to retrieve variances across all periods
        /// - Pass periodId as a query parameter to filter variances to a specific fiscal period
        /// - Combine with account and period lookups for detailed variance analysis
        ///
        /// **Business Rules:**
        /// - Variance is calculated as BudgetQuantity minus ActualQuantity
        /// - VariancePercent represents the variance as a percentage of the budget
        /// - IsFavorable indicates whether the variance is positive for the organization
        /// - When periodId is null, variances for all periods are returned
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="periodId">Optional fiscal period ID to filter variances. If omitted, returns variances for all periods.</param>
        /// <returns>List of budget vs actual variance records</returns>
        /// <response code="200">Returns the list of budget variances</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("variances")]
        public async Task<ActionResult<IReadOnlyList<BudgetVarianceDto>>> GetVariances([FromQuery] Guid? periodId)
        {
            var variances = await _budgetService.GetVariancesAsync(periodId);
            return Ok(variances);
        }

        /// <summary>
        /// Creates a new budget entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Setting up a new budget allocation for a unit account in a specific fiscal period
        /// - Creating initial budgets during the annual budget planning process
        /// - Adding budget entries for new accounts mid-year
        ///
        /// **Integration Pattern:**
        /// - Supply the unit account ID, fiscal year ID, and fiscal period ID from their respective endpoints
        /// - On success, returns 201 Created with the new budget in the response body and a Location header
        /// - On validation failure (e.g., invalid account, duplicate budget), returns 400 Bad Request
        ///
        /// **Business Rules:**
        /// - UnitAccountId, FiscalYearId, and FiscalPeriodId must reference existing records
        /// - BudgetQuantity represents the budgeted amount for the specified period
        /// - BudgetVersion defaults if not provided; use it to maintain multiple budget scenarios (e.g., "Original", "Revised")
        /// - Duplicate budget entries for the same account, period, and version are not allowed
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The budget creation data including account, period, and amount details</param>
        /// <returns>The newly created unit account budget</returns>
        /// <response code="201">Budget created successfully</response>
        /// <response code="400">Validation error - invalid input or duplicate budget entry</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        public async Task<ActionResult<UnitAccountBudgetDto>> Create([FromBody] CreateUnitAccountBudgetDto dto)
        {
            try
            {
                var budget = await _budgetService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = budget.Id }, budget);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing budget.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Revising budget amounts during mid-year budget adjustments
        /// - Deactivating a budget entry that is no longer applicable
        /// - Adding or updating notes on an existing budget for audit purposes
        ///
        /// **Integration Pattern:**
        /// - Fetch the current budget using GetById, modify the desired fields, and submit the update
        /// - The ID in the URL path identifies the budget; the request body contains the updated fields
        /// - Returns 404 if the budget ID does not exist
        ///
        /// **Business Rules:**
        /// - Only BudgetQuantity, Notes, and IsActive can be updated
        /// - The associated account, fiscal year, and fiscal period cannot be changed (create a new entry instead)
        /// - Setting IsActive to false effectively soft-deletes the budget without removing it
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the budget to update</param>
        /// <param name="dto">The updated budget data including quantity, notes, and active status</param>
        /// <returns>The updated unit account budget</returns>
        /// <response code="200">Budget updated successfully</response>
        /// <response code="404">Budget with the specified ID was not found</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id}")]
        public async Task<ActionResult<UnitAccountBudgetDto>> Update(Guid id, [FromBody] UpdateUnitAccountBudgetDto dto)
        {
            try
            {
                var budget = await _budgetService.UpdateAsync(id, dto);
                return Ok(budget);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a budget entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing an erroneously created budget entry
        /// - Cleaning up draft budgets that were never finalized
        /// - Deleting budget entries for accounts that have been removed
        ///
        /// **Integration Pattern:**
        /// - Pass the budget ID in the URL path to delete it
        /// - Returns 204 No Content on successful deletion regardless of whether the ID existed
        /// - Consider using the Update endpoint to set IsActive to false for a soft-delete approach instead
        ///
        /// **Business Rules:**
        /// - This is a hard delete; the budget record is permanently removed
        /// - Prefer soft-delete (setting IsActive to false via Update) if audit trail must be preserved
        /// - Deleting a budget does not affect actual transaction records or journal entries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier of the budget entry to delete</param>
        /// <returns>No content on successful deletion</returns>
        /// <response code="204">Budget deleted successfully</response>
        /// <response code="401">Unauthorized - user is not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            await _budgetService.DeleteAsync(id);
            return NoContent();
        }
    }
}
