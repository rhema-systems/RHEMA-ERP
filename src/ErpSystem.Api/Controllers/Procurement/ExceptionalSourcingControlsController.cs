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
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> Prepare(Guid tenderId, PrepareProcurementExceptionalSourcingRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.PrepareAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/submit")]
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> SubmitApproval(Guid tenderId, SubmitProcurementExceptionalApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/decision")]
    [Authorize(Policy = "procurement.tender.approve")]
    public Task<IActionResult> DecideApproval(Guid tenderId, DecideProcurementExceptionalApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.DecideApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("negotiation")]
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> RecordNegotiation(Guid tenderId, RecordProcurementExceptionalNegotiationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordNegotiationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("recommendation")]
    [Authorize(Policy = "procurement.tender.evaluate")]
    public Task<IActionResult> RecordRecommendation(Guid tenderId, RecordProcurementExceptionalRecommendationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordRecommendationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("award")]
    [Authorize]
    public Task<IActionResult> RecordAward(Guid tenderId, RecordProcurementTenderAwardRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAwardAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("contract")]
    [Authorize(Policy = "procurement.contract.manage")]
    public Task<IActionResult> RecordContract(Guid tenderId, RecordProcurementTenderContractRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordContractAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("acceptance")]
    [Authorize(Policy = "procurement.contract.manage")]
    public Task<IActionResult> RecordAcceptance(Guid tenderId, RecordProcurementTenderAcceptanceRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAcceptanceAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("post-award-filing")]
    [Authorize(Policy = "procurement.contract.manage")]
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
        catch (ProcurementAwardReadinessNotFoundException exception)
        { return NotFound(ReadinessProblem(404, exception.Code, exception.Message)); }
        catch (ProcurementAwardReadinessAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, ReadinessProblem(403, "AWARD_READINESS_ACCESS_FORBIDDEN", exception.Message)); }
        catch (ProcurementAwardReadinessConflictException exception)
        { return Conflict(ReadinessProblem(409, exception.Code, exception.Message)); }
        catch (ProcurementAwardReadinessBlockedException exception)
        { return UnprocessableEntity(ReadinessProblem(422, exception.Code, exception.Message, exception.Decision)); }
        catch (ProcurementAwardReadinessValidationException exception)
        { return UnprocessableEntity(ReadinessProblem(422, exception.Code, exception.Message)); }
    }

    private object ReadinessProblem(int status, string code, string message, object? decision = null) => new
    {
        status,
        title = status == 422 ? "Award is not ready" : "Award-readiness check failed",
        detail = message,
        instance = Request.Path.Value,
        code,
        correlationId = Correlation(),
        decision
    };
}
