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
        /// Get all unit account budgets.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetAll()
        {
            var budgets = await _budgetService.GetAllAsync();
            return Ok(budgets);
        }

        /// <summary>
        /// Get a specific budget by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<UnitAccountBudgetDto>> GetById(Guid id)
        {
            var budget = await _budgetService.GetByIdAsync(id);
            if (budget == null)
                return NotFound(new { error = $"Budget with ID '{id}' not found" });
            return Ok(budget);
        }

        /// <summary>
        /// Get all budgets for a specific unit account.
        /// </summary>
        [HttpGet("by-account/{accountId}")]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetByAccount(Guid accountId)
        {
            var budgets = await _budgetService.GetByAccountAsync(accountId);
            return Ok(budgets);
        }

        /// <summary>
        /// Get all budgets for a specific fiscal period.
        /// </summary>
        [HttpGet("by-period/{periodId}")]
        public async Task<ActionResult<IReadOnlyList<UnitAccountBudgetDto>>> GetByPeriod(Guid periodId)
        {
            var budgets = await _budgetService.GetByPeriodAsync(periodId);
            return Ok(budgets);
        }

        /// <summary>
        /// Get budget vs actual variances.
        /// </summary>
        [HttpGet("variances")]
        public async Task<ActionResult<IReadOnlyList<BudgetVarianceDto>>> GetVariances([FromQuery] Guid? periodId)
        {
            var variances = await _budgetService.GetVariancesAsync(periodId);
            return Ok(variances);
        }

        /// <summary>
        /// Create a new budget entry.
        /// </summary>
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
        /// Update an existing budget.
        /// </summary>
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
        /// Delete a budget entry.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            await _budgetService.DeleteAsync(id);
            return NoContent();
        }
    }
}
