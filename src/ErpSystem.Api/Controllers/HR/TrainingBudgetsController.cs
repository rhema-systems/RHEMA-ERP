using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-budgets")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingBudgetsController : ControllerBase
{
    private readonly ITrainingBudgetService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingBudgetsController(ITrainingBudgetService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingBudgetDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("code/{budgetCode}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingBudgetDto?>> GetByBudgetCode(string budgetCode, CancellationToken ct)
        => Ok(await _service.GetByBudgetCodeAsync(budgetCode, ct));

    [HttpGet("year/{year:int}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetByYear(int year, CancellationToken ct)
        => Ok(await _service.GetByYearAsync(year, ct));

    [HttpGet("year/{year:int}/quarter/{quarter:int}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetByYearAndQuarter(int year, int quarter, CancellationToken ct)
        => Ok(await _service.GetByYearAndQuarterAsync(year, quarter, ct));

    [HttpGet("approved")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetApproved(CancellationToken ct)
        => Ok(await _service.GetApprovedAsync(ct));

    [HttpGet("over-budget")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetOverBudget(CancellationToken ct)
        => Ok(await _service.GetOverBudgetAsync(ct));

    [HttpGet("org-unit/{orgUnitId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetSummaryDto>>> GetByOrganizationUnitId(Guid orgUnitId, CancellationToken ct)
        => Ok(await _service.GetByOrganizationUnitIdAsync(orgUnitId, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingBudgetDto>> Create([FromBody] CreateTrainingBudgetDto dto, CancellationToken ct)
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
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingBudgetDto>> Update(Guid id, [FromBody] UpdateTrainingBudgetDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTrainingBudgetDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.BudgetId = id;
        await _service.ApproveAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Training budget approved." });
    }

    // =========================================================================
    // TRANSACTIONS
    // =========================================================================

    [HttpPost("{id:guid}/transactions")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingBudgetTransactionDto>> RecordTransaction(Guid id, [FromBody] CreateTrainingBudgetTransactionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.BudgetId = id;
        return Ok(await _service.RecordTransactionAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/transactions")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetTransactionDto>>> GetTransactions(Guid id, CancellationToken ct)
        => Ok(await _service.GetTransactionsAsync(id, ct));

    [HttpGet("{id:guid}/transactions/date-range")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingBudgetTransactionDto>>> GetTransactionsByDateRange(
        Guid id,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct)
        => Ok(await _service.GetTransactionsByDateRangeAsync(id, from, to, ct));
}
