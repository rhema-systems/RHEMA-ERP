using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/tenders/{tenderId:guid}/exception-controls")]
public sealed class ExceptionalSourcingControlsController : ControllerBase
{
    private readonly IProcurementExceptionalSourcingControlService _service;

    public ExceptionalSourcingControlsController(IProcurementExceptionalSourcingControlService service) => _service = service;

    [HttpGet]
    public Task<IActionResult> Get(Guid tenderId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetAsync(tenderId, cancellationToken));

    [HttpGet("readiness")]
    public Task<IActionResult> GetReadiness(Guid tenderId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetReadinessAsync(tenderId, cancellationToken));

    [HttpPost("prepare")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> Prepare(Guid tenderId, PrepareProcurementExceptionalSourcingRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.PrepareAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/submit")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> SubmitApproval(Guid tenderId, SubmitProcurementExceptionalApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/decision")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> DecideApproval(Guid tenderId, DecideProcurementExceptionalApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.DecideApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("negotiation")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> RecordNegotiation(Guid tenderId, RecordProcurementExceptionalNegotiationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordNegotiationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("recommendation")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> RecordRecommendation(Guid tenderId, RecordProcurementExceptionalRecommendationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordRecommendationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("award")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> RecordAward(Guid tenderId, RecordProcurementTenderAwardRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAwardAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("contract")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> RecordContract(Guid tenderId, RecordProcurementTenderContractRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordContractAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("acceptance")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> RecordAcceptance(Guid tenderId, RecordProcurementTenderAcceptanceRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAcceptanceAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("post-award-filing")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> RecordPostAwardFiling(Guid tenderId, RecordProcurementPostAwardFilingRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordPostAwardFilingAsync(tenderId, request, Correlation(), cancellationToken));

    private string Correlation() =>
        Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString() : Guid.NewGuid().ToString("N");

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ProcurementExceptionalSourcingNotFoundException exception)
        { return NotFound(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementExceptionalSourcingConflictException exception)
        { return Conflict(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementExceptionalSourcingValidationException exception)
        { return UnprocessableEntity(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementExceptionalSourcingAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, new { code = "EXCEPTIONAL_SOURCING_FORBIDDEN", message = exception.Message }); }
    }
}
