using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public sealed class EmergencyProcurementPlansController : ControllerBase
{
    private readonly IEmergencyProcurementPlanService _service;

    public EmergencyProcurementPlansController(IEmergencyProcurementPlanService service) => _service = service;

    [HttpGet]
    public Task<IActionResult> GetPlans(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? emergencyType = null) =>
        ExecuteAsync(() => _service.GetPlansAsync(page, pageSize, search, status, emergencyType));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id)
    {
        var value = await _service.GetByIdAsync(id);
        return value is null ? NotFound(Problem(404, "EMERGENCY_PLAN_NOT_FOUND", "The emergency plan was not found.")) : Ok(value);
    }

    [HttpGet("by-number/{planNumber}")]
    public async Task<IActionResult> GetByPlanNumber(string planNumber)
    {
        var value = await _service.GetByPlanNumberAsync(planNumber);
        return value is null ? NotFound(Problem(404, "EMERGENCY_PLAN_NOT_FOUND", "The emergency plan was not found.")) : Ok(value);
    }

    [HttpGet("type/{emergencyType}")]
    public Task<IActionResult> GetByType(string emergencyType) =>
        ExecuteAsync(() => _service.GetByEmergencyTypeAsync(emergencyType));

    [HttpGet("active")]
    public Task<IActionResult> GetActivePlans() => ExecuteAsync(_service.GetActivePlansAsync);

    [HttpGet("triggered")]
    public Task<IActionResult> GetTriggeredPlans() => ExecuteAsync(_service.GetTriggeredPlansAsync);

    [HttpGet("governance/options")]
    public Task<IActionResult> GetGovernanceOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetGovernanceOptionsAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreateEmergencyProcurementPlanDto dto)
    {
        try
        {
            var value = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetPlan), new { id = value.Id }, value);
        }
        catch (EmergencyPurchaseValidationException exception)
        { return UnprocessableEntity(Problem(422, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseConflictException exception)
        { return Conflict(Problem(409, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseAuthorizationException exception)
        { return StatusCode(403, Problem(403, "EMERGENCY_PURCHASE_FORBIDDEN", exception.Message)); }
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> UpdatePlan(Guid id, [FromBody] CreateEmergencyProcurementPlanDto dto) =>
        ExecuteAsync(() => _service.UpdateAsync(id, dto));

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> DeletePlan(Guid id) => ExecuteNoContentAsync(() => _service.DeleteAsync(id));

    // Compatibility endpoints remain explicit hard denials in the service so
    // stale clients cannot bypass the governed lifecycle.
    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> ActivatePlan(Guid id) => ExecuteAsync(() => _service.ActivateAsync(id));

    [HttpPost("{id:guid}/trigger")]
    public Task<IActionResult> TriggerPlan(Guid id) => ExecuteAsync(() => _service.TriggerAsync(id));

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> DeactivatePlan(Guid id) => ExecuteAsync(() => _service.DeactivateAsync(id));

    [HttpPost("{id:guid}/exception/prepare")]
    public Task<IActionResult> PrepareException(Guid id, PrepareEmergencyPurchaseRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.PrepareExceptionAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/audit/submit")]
    public Task<IActionResult> SubmitForAudit(Guid id, EmergencyPurchaseLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitForAuditAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/audit/vouch")]
    [Authorize(Roles = "TDC_INTERNAL_AUDIT")]
    public Task<IActionResult> Vouch(Guid id, VouchEmergencyPurchaseRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.VouchAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/approval/submit")]
    public Task<IActionResult> SubmitForApproval(Guid id, EmergencyPurchaseLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitForApprovalAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/approval/decision")]
    [Authorize(Roles = "TDC_MANAGING_DIRECTOR,TDC_BOARD_APPROVER")]
    public Task<IActionResult> Decide(Guid id, DecideEmergencyPurchaseRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.DecideAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/trigger")]
    public Task<IActionResult> TriggerGoverned(Guid id, EmergencyPurchaseLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.TriggerGovernedAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{id:guid}/exception/post-award")]
    public Task<IActionResult> FilePostAward(Guid id, FileEmergencyPurchasePostAwardRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.FilePostAwardAsync(id, request, Correlation(), cancellationToken));

    [HttpPost("{planId:guid}/items")]
    public Task<IActionResult> AddItem(Guid planId, [FromBody] CreateEmergencyProcurementItemDto dto) =>
        ExecuteAsync(() => _service.AddItemAsync(planId, dto));

    [HttpPut("items/{itemId:guid}")]
    public Task<IActionResult> UpdateItem(Guid itemId, [FromBody] CreateEmergencyProcurementItemDto dto) =>
        ExecuteAsync(() => _service.UpdateItemAsync(itemId, dto));

    [HttpDelete("items/{itemId:guid}")]
    public Task<IActionResult> DeleteItem(Guid itemId) => ExecuteNoContentAsync(() => _service.DeleteItemAsync(itemId));

    [HttpGet("{planId:guid}/items/critical")]
    public Task<IActionResult> GetCriticalItems(Guid planId) => ExecuteAsync(() => _service.GetCriticalItemsAsync(planId));

    [HttpPost("{planId:guid}/suppliers")]
    public Task<IActionResult> AddSupplier(Guid planId, [FromBody] CreateEmergencySupplierDto dto) =>
        ExecuteAsync(() => _service.AddSupplierAsync(planId, dto));

    [HttpPut("suppliers/{supplierId:guid}")]
    public Task<IActionResult> UpdateSupplier(Guid supplierId, [FromBody] CreateEmergencySupplierDto dto) =>
        ExecuteAsync(() => _service.UpdateSupplierAsync(supplierId, dto));

    [HttpDelete("suppliers/{supplierId:guid}")]
    public Task<IActionResult> DeleteSupplier(Guid supplierId) => ExecuteNoContentAsync(() => _service.DeleteSupplierAsync(supplierId));

    [HttpGet("{planId:guid}/suppliers/active")]
    public Task<IActionResult> GetActiveSuppliers(Guid planId) => ExecuteAsync(() => _service.GetActiveSuppliersAsync(planId));

    [HttpGet("suppliers/category/{category}")]
    public Task<IActionResult> GetSuppliersByCategory(string category) => ExecuteAsync(() => _service.GetSuppliersByCategoryAsync(category));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (EmergencyPurchaseNotFoundException exception)
        { return NotFound(Problem(404, exception.Code, exception.Message)); }
        catch (KeyNotFoundException exception)
        { return NotFound(Problem(404, "EMERGENCY_RESOURCE_NOT_FOUND", exception.Message)); }
        catch (EmergencyPurchaseConflictException exception)
        { return Conflict(Problem(409, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseValidationException exception)
        { return UnprocessableEntity(Problem(422, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseAuthorizationException exception)
        { return StatusCode(403, Problem(403, "EMERGENCY_PURCHASE_FORBIDDEN", exception.Message)); }
    }

    private async Task<IActionResult> ExecuteNoContentAsync(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (EmergencyPurchaseNotFoundException exception)
        { return NotFound(Problem(404, exception.Code, exception.Message)); }
        catch (KeyNotFoundException exception)
        { return NotFound(Problem(404, "EMERGENCY_RESOURCE_NOT_FOUND", exception.Message)); }
        catch (EmergencyPurchaseConflictException exception)
        { return Conflict(Problem(409, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseValidationException exception)
        { return UnprocessableEntity(Problem(422, exception.Code, exception.Message)); }
        catch (EmergencyPurchaseAuthorizationException exception)
        { return StatusCode(403, Problem(403, "EMERGENCY_PURCHASE_FORBIDDEN", exception.Message)); }
    }

    private object Problem(int status, string code, string detail) => new
    {
        type = $"https://tdc.gov.gh/problems/{code.ToLowerInvariant().Replace('_', '-')}",
        title = status == 403 ? "Emergency-purchase access forbidden" : "Emergency-purchase request failed",
        status,
        detail,
        instance = Request.Path.Value,
        code,
        correlationId = Correlation()
    };

    private string Correlation() =>
        Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString() : HttpContext.TraceIdentifier;
}
