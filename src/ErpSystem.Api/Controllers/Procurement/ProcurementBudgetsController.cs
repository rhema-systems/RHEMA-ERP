using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class ProcurementBudgetsController : ControllerBase
{
    private readonly IProcurementBudgetService _budgetService;
    private readonly ILogger<ProcurementBudgetsController> _logger;

    public ProcurementBudgetsController(
        IProcurementBudgetService budgetService,
        ILogger<ProcurementBudgetsController> logger)
    {
        _budgetService = budgetService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcurementBudgetDto>>> GetBudgets(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] Guid? departmentId = null, [FromQuery] int? fiscalYear = null)
    {
        try
        {
            var result = await _budgetService.GetBudgetsAsync(page, pageSize, search, status, departmentId, fiscalYear);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement budgets");
            return StatusCode(500, "An error occurred while retrieving procurement budgets");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcurementBudgetDetailDto>> GetBudget(Guid id)
    {
        try
        {
            var budget = await _budgetService.GetByIdAsync(id);
            if (budget == null) return NotFound($"Budget with ID {id} not found");
            return Ok(budget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget {BudgetId}", id);
            return StatusCode(500, "An error occurred while retrieving the budget");
        }
    }

    [HttpGet("by-code/{budgetCode}")]
    public async Task<ActionResult<ProcurementBudgetDto>> GetByBudgetCode(string budgetCode)
    {
        try
        {
            var budget = await _budgetService.GetByBudgetCodeAsync(budgetCode);
            if (budget == null) return NotFound($"Budget with code {budgetCode} not found");
            return Ok(budget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget by code {BudgetCode}", budgetCode);
            return StatusCode(500, "An error occurred while retrieving the budget");
        }
    }

    [HttpGet("department/{departmentId}")]
    public async Task<ActionResult<IEnumerable<ProcurementBudgetDto>>> GetByDepartment(Guid departmentId)
    {
        try
        {
            var budgets = await _budgetService.GetByDepartmentAsync(departmentId);
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budgets for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving budgets");
        }
    }

    [HttpGet("fiscal-year/{fiscalYear}")]
    public async Task<ActionResult<IEnumerable<ProcurementBudgetDto>>> GetByFiscalYear(int fiscalYear)
    {
        try
        {
            var budgets = await _budgetService.GetByFiscalYearAsync(fiscalYear);
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budgets for fiscal year {FiscalYear}", fiscalYear);
            return StatusCode(500, "An error occurred while retrieving budgets");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ProcurementBudgetDto>>> GetActiveBudgets()
    {
        try
        {
            var budgets = await _budgetService.GetActiveBudgetsAsync();
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active budgets");
            return StatusCode(500, "An error occurred while retrieving active budgets");
        }
    }

    /// <summary>
    /// Gets available budgets for linking to a procurement plan
    /// </summary>
    [HttpGet("available-for-linking")]
    public async Task<ActionResult<IEnumerable<ProcurementBudgetDto>>> GetAvailableBudgetsForLinking(
        [FromQuery] Guid departmentId, [FromQuery] int fiscalYear)
    {
        try
        {
            var budgets = await _budgetService.GetAvailableBudgetsForLinkingAsync(departmentId, fiscalYear);
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available budgets for linking");
            return StatusCode(500, "An error occurred while retrieving available budgets");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcurementBudgetDetailDto>> CreateBudget([FromBody] CreateProcurementBudgetDto dto)
    {
        try
        {
            var budget = await _budgetService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBudget), new { id = budget.Id }, budget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating budget");
            return StatusCode(500, "An error occurred while creating the budget");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProcurementBudgetDetailDto>> UpdateBudget(Guid id, [FromBody] CreateProcurementBudgetDto dto)
    {
        try
        {
            var budget = await _budgetService.UpdateAsync(id, dto);
            return Ok(budget);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating budget {BudgetId}", id);
            return StatusCode(500, "An error occurred while updating the budget");
        }
    }

    [HttpPost("{id}/submit")]
    public async Task<ActionResult<ProcurementBudgetDetailDto>> SubmitBudget(Guid id)
    {
        try
        {
            var budget = await _budgetService.SubmitForApprovalAsync(id);
            return Ok(budget);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting budget {BudgetId}", id);
            return StatusCode(500, "An error occurred while submitting the budget for approval");
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ProcurementBudgetDetailDto>> ApproveBudget(
        Guid id,
        [FromBody] ApproveProcurementBudgetDto? dto)
    {
        try
        {
            var budget = await _budgetService.ApproveAsync(id, dto ?? new ApproveProcurementBudgetDto());
            return Ok(budget);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing budget workflow decision {BudgetId}", id);
            return StatusCode(500, "An error occurred while processing the budget workflow decision");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteBudget(Guid id)
    {
        try
        {
            await _budgetService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting budget {BudgetId}", id);
            return StatusCode(500, "An error occurred while deleting the budget");
        }
    }

    [HttpPost("{budgetId}/allocations")]
    public async Task<ActionResult<ProcurementBudgetAllocationDto>> AddAllocation(Guid budgetId, [FromBody] CreateProcurementBudgetAllocationDto dto)
    {
        try
        {
            var allocation = await _budgetService.AddAllocationAsync(budgetId, dto);
            return Ok(allocation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding allocation to budget {BudgetId}", budgetId);
            return StatusCode(500, "An error occurred while adding the allocation");
        }
    }

    [HttpPut("allocations/{allocationId}")]
    public async Task<ActionResult<ProcurementBudgetAllocationDto>> UpdateAllocation(Guid allocationId, [FromBody] CreateProcurementBudgetAllocationDto dto)
    {
        try
        {
            var allocation = await _budgetService.UpdateAllocationAsync(allocationId, dto);
            return Ok(allocation);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating allocation {AllocationId}", allocationId);
            return StatusCode(500, "An error occurred while updating the allocation");
        }
    }

    [HttpDelete("allocations/{allocationId}")]
    public async Task<ActionResult> DeleteAllocation(Guid allocationId)
    {
        try
        {
            await _budgetService.DeleteAllocationAsync(allocationId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting allocation {AllocationId}", allocationId);
            return StatusCode(500, "An error occurred while deleting the allocation");
        }
    }

    [HttpPost("{budgetId}/revisions")]
    public async Task<ActionResult<ProcurementBudgetRevisionDto>> CreateRevision(Guid budgetId, [FromBody] CreateProcurementBudgetRevisionDto dto)
    {
        try
        {
            var revision = await _budgetService.CreateRevisionAsync(budgetId, dto);
            return Ok(revision);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revision for budget {BudgetId}", budgetId);
            return StatusCode(500, "An error occurred while creating the revision");
        }
    }

    [HttpPost("revisions/{revisionId}/approve")]
    public async Task<ActionResult<ProcurementBudgetRevisionDto>> ApproveRevision(Guid revisionId)
    {
        try
        {
            var revision = await _budgetService.ApproveRevisionAsync(revisionId);
            return Ok(revision);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving revision {RevisionId}", revisionId);
            return StatusCode(500, "An error occurred while approving the revision");
        }
    }

    [HttpPost("revisions/{revisionId}/reject")]
    public async Task<ActionResult<ProcurementBudgetRevisionDto>> RejectRevision(Guid revisionId, [FromBody] string reason)
    {
        try
        {
            var revision = await _budgetService.RejectRevisionAsync(revisionId, reason);
            return Ok(revision);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting revision {RevisionId}", revisionId);
            return StatusCode(500, "An error occurred while rejecting the revision");
        }
    }

    [HttpGet("{budgetId}/revisions")]
    public async Task<ActionResult<IEnumerable<ProcurementBudgetRevisionDto>>> GetRevisions(Guid budgetId)
    {
        try
        {
            var revisions = await _budgetService.GetRevisionsAsync(budgetId);
            return Ok(revisions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revisions for budget {BudgetId}", budgetId);
            return StatusCode(500, "An error occurred while retrieving revisions");
        }
    }

    [HttpGet("department/{departmentId}/fiscal-year/{fiscalYear}/total-allocated")]
    public async Task<ActionResult<decimal>> GetTotalAllocated(Guid departmentId, int fiscalYear)
    {
        try
        {
            var total = await _budgetService.GetTotalAllocatedAsync(departmentId, fiscalYear);
            return Ok(total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total allocated for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving total allocated");
        }
    }

    [HttpGet("department/{departmentId}/fiscal-year/{fiscalYear}/total-utilized")]
    public async Task<ActionResult<decimal>> GetTotalUtilized(Guid departmentId, int fiscalYear)
    {
        try
        {
            var total = await _budgetService.GetTotalUtilizedAsync(departmentId, fiscalYear);
            return Ok(total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total utilized for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving total utilized");
        }
    }

    [HttpPost("{budgetId}/record-utilization")]
    public async Task<ActionResult> RecordUtilization(Guid budgetId, [FromQuery] decimal amount, [FromQuery] string? category = null)
    {
        try
        {
            await _budgetService.RecordUtilizationAsync(budgetId, amount, category);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording utilization for budget {BudgetId}", budgetId);
            return StatusCode(500, "An error occurred while recording utilization");
        }
    }
}
