using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-plans")]
[Authorize]
public class TrainingPlansController : ControllerBase
{
    private readonly ITrainingPlanService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingPlansController(ITrainingPlanService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingPlanSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingPlanDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{planNumber}")]
    public async Task<ActionResult<TrainingPlanDto?>> GetByPlanNumber(string planNumber, CancellationToken ct)
        => Ok(await _service.GetByPlanNumberAsync(planNumber, ct));

    [HttpGet("year/{year:int}")]
    public async Task<ActionResult<IEnumerable<TrainingPlanSummaryDto>>> GetByYear(int year, CancellationToken ct)
        => Ok(await _service.GetByYearAsync(year, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<TrainingPlanSummaryDto>>> GetByStatus(TrainingPlanStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingPlanDto>> Create([FromBody] CreateTrainingPlanDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingPlanDto>> Update(Guid id, [FromBody] UpdateTrainingPlanDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SubmitForApprovalAsync(id, employeeId.Value, ct);
        return Ok(new { message = "Training plan submitted for approval." });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTrainingPlanDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.PlanId = id;
        await _service.ApproveAsync(dto, ct);
        return Ok(new { message = "Training plan approved." });
    }

    // =========================================================================
    // ITEMS
    // =========================================================================

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<TrainingPlanItemDto>> AddItem(Guid id, [FromBody] CreateTrainingPlanItemDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.PlanId = id;
        return Ok(await _service.AddItemAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<IEnumerable<TrainingPlanItemDto>>> GetItems(Guid id, CancellationToken ct)
        => Ok(await _service.GetItemsAsync(id, ct));

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<TrainingPlanItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateTrainingPlanItemDto dto, CancellationToken ct)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateItemAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken ct)
    {
        await _service.DeleteItemAsync(itemId, ct);
        return NoContent();
    }

    // =========================================================================
    // BUDGET LINES
    // =========================================================================

    [HttpPost("{id:guid}/budget-lines")]
    public async Task<ActionResult<TrainingPlanBudgetLineDto>> AddBudgetLine(Guid id, [FromBody] CreateTrainingPlanBudgetLineDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.PlanId = id;
        return Ok(await _service.AddBudgetLineAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/budget-lines")]
    public async Task<ActionResult<IEnumerable<TrainingPlanBudgetLineDto>>> GetBudgetLines(Guid id, CancellationToken ct)
        => Ok(await _service.GetBudgetLinesAsync(id, ct));

    [HttpPut("budget-lines/{lineId:guid}")]
    public async Task<ActionResult<TrainingPlanBudgetLineDto>> UpdateBudgetLine(Guid lineId, [FromBody] UpdateTrainingPlanBudgetLineDto dto, CancellationToken ct)
    {
        if (lineId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateBudgetLineAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("budget-lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteBudgetLine(Guid lineId, CancellationToken ct)
    {
        await _service.DeleteBudgetLineAsync(lineId, ct);
        return NoContent();
    }
}
