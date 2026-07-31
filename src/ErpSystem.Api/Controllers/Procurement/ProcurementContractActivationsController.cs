using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/contract-activations")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementContractActivationsController : ControllerBase
{
    private readonly IProcurementContractActivationService _service;

    public ProcurementContractActivationsController(
        IProcurementContractActivationService service)
    {
        _service = service;
    }

    [HttpGet("contracts/{contractId:guid}")]
    public Task<IActionResult> GetOverview(
        Guid contractId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            contractId, cancellationToken)));

    [HttpPost("contracts/{contractId:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid contractId,
        [FromBody] SubmitProcurementContractActivationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var activation = await _service.SubmitAsync(
                contractId, request, CorrelationId, cancellationToken);
            return Created(
                $"/api/procurement/contract-activations/{activation.Id}",
                activation);
        });

    [HttpPost("{activationId:guid}/decision")]
    public Task<IActionResult> Decide(
        Guid activationId,
        [FromBody] DecideProcurementContractActivationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.DecideAsync(
            activationId, request, CorrelationId, cancellationToken)));

    [HttpPost("{activationId:guid}/activate")]
    public Task<IActionResult> Activate(
        Guid activationId,
        [FromBody] ActivateProcurementContractRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ActivateAsync(
            activationId, request, CorrelationId, cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementContractActivationNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Contract activation not found", exception.Message));
        }
        catch (ProcurementContractActivationAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "CONTRACT_ACTIVATION_ACCESS_FORBIDDEN",
                    "Contract activation access forbidden", exception.Message));
        }
        catch (ProcurementContractActivationConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Contract activation conflict", exception.Message));
        }
        catch (ProcurementContractActivationValidationException exception)
        {
            return UnprocessableEntity(Problem(422, exception.Code,
                "Contract activation validation failed", exception.Message));
        }
    }

    private static ProblemDetails Problem(
        int status, string code, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions = { ["code"] = code }
    };
}
