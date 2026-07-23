using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/prequalification")]
public sealed class PrequalificationController : ControllerBase
{
    private readonly IProcurementPrequalificationService _service;

    public PrequalificationController(IProcurementPrequalificationService service) => _service = service;

    [HttpGet]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.ListAsync(cancellationToken));

    [HttpGet("readiness")]
    public Task<IActionResult> GetReadiness(CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetReadinessAsync(cancellationToken));

    [HttpGet("{exerciseId:guid}")]
    public Task<IActionResult> Get(Guid exerciseId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetAsync(exerciseId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> Create(CreateProcurementPrequalificationExerciseRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.CreateAsync(request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/advertise")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> Advertise(Guid exerciseId, AdvertiseProcurementPrequalificationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.AdvertiseAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/applications")]
    public Task<IActionResult> SubmitApplication(Guid exerciseId, SubmitProcurementPrequalificationApplicationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitApplicationAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/close")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> Close(Guid exerciseId, CloseProcurementPrequalificationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.CloseAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/applications/{applicationId:guid}/evaluate")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> Evaluate(Guid exerciseId, Guid applicationId, EvaluateProcurementPrequalificationApplicationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.EvaluateAsync(exerciseId, applicationId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/decision/submit")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> SubmitDecision(Guid exerciseId, SubmitProcurementPrequalificationDecisionRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitDecisionAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/decision")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public Task<IActionResult> Decide(Guid exerciseId, DecideProcurementPrequalificationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.DecideAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpPost("{exerciseId:guid}/expire")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public Task<IActionResult> Expire(Guid exerciseId, ExpireProcurementPrequalificationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.ExpireAsync(exerciseId, request, Correlation(), cancellationToken));

    [HttpGet("eligibility")]
    public Task<IActionResult> CheckEligibility(
        [FromQuery] Guid businessPartnerId,
        [FromQuery] Guid categoryId,
        [FromQuery] DateTime? atUtc,
        CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.CheckEligibilityAsync(businessPartnerId, categoryId, atUtc, cancellationToken));

    private string Correlation() =>
        Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString() : Guid.NewGuid().ToString("N");

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ProcurementPrequalificationNotFoundException exception)
        { return NotFound(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementPrequalificationConflictException exception)
        { return Conflict(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementPrequalificationValidationException exception)
        { return UnprocessableEntity(new { code = exception.Code, message = exception.Message }); }
        catch (ProcurementPrequalificationAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, new { code = "PREQUALIFICATION_FORBIDDEN", message = exception.Message }); }
    }
}
