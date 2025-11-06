using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/expenses")]
[Authorize]
public class MaintenanceExpenseController : ControllerBase
{
    private readonly IMaintenanceExpenseService _expenseService;
    private readonly ILogger<MaintenanceExpenseController> _logger;

    public MaintenanceExpenseController(
        IMaintenanceExpenseService expenseService,
        ILogger<MaintenanceExpenseController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateMaintenanceExpenseDto createDto)
    {
        try
        {
            var result = await _expenseService.CreateExpenseAsync(createDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return BadRequest(new { message = "Failed to create expense", error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateMaintenanceExpenseDto updateDto)
    {
        try
        {
            var result = await _expenseService.UpdateExpenseAsync(id, updateDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            return BadRequest(new { message = "Failed to update expense", error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return BadRequest(new { message = "Failed to delete expense", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExpenseById(Guid id)
    {
        try
        {
            var result = await _expenseService.GetExpenseByIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", id);
            return BadRequest(new { message = "Failed to get expense", error = ex.Message });
        }
    }

    [HttpGet("by-workorder/{workOrderId}")]
    public async Task<IActionResult> GetExpensesByWorkOrder(Guid workOrderId)
    {
        try
        {
            var results = await _expenseService.GetExpensesByWorkOrderIdAsync(workOrderId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for work order {WorkOrderId}", workOrderId);
            return BadRequest(new { message = "Failed to get expenses", error = ex.Message });
        }
    }

    [HttpGet("by-technician/{technicianId}")]
    public async Task<IActionResult> GetExpensesByTechnician(Guid technicianId)
    {
        try
        {
            var results = await _expenseService.GetExpensesByTechnicianIdAsync(technicianId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for technician {TechnicianId}", technicianId);
            return BadRequest(new { message = "Failed to get expenses", error = ex.Message });
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingExpenses()
    {
        try
        {
            var results = await _expenseService.GetPendingExpensesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            return BadRequest(new { message = "Failed to get pending expenses", error = ex.Message });
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveExpense(Guid id, [FromBody] ApproveExpenseDto approveDto)
    {
        try
        {
            var result = await _expenseService.ApproveExpenseAsync(id, approveDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            return BadRequest(new { message = "Failed to approve expense", error = ex.Message });
        }
    }

    [HttpGet("total-by-workorder/{workOrderId}")]
    public async Task<IActionResult> GetTotalExpensesByWorkOrder(Guid workOrderId)
    {
        try
        {
            var total = await _expenseService.GetTotalExpensesByWorkOrderIdAsync(workOrderId);
            return Ok(new { workOrderId, totalAmount = total });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total expenses for work order {WorkOrderId}", workOrderId);
            return BadRequest(new { message = "Failed to get total expenses", error = ex.Message });
        }
    }
}
