using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/contract-operations")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementContractOperationsController : ControllerBase
{
    private readonly IProcurementContractOperationsService _service;

    public ProcurementContractOperationsController(
        IProcurementContractOperationsService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementContractOperationsSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(
            request, CorrelationId, cancellationToken)));

    [HttpGet("{contractId:guid}")]
    public Task<IActionResult> Get(
        Guid contractId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(
            contractId, CorrelationId, cancellationToken)));

    [HttpPost("process-alerts")]
    public Task<IActionResult> ProcessAlerts(
        [FromBody] ProcessProcurementContractOperationsAlertsRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ProcessAlertsAsync(
            request, CorrelationId, cancellationToken)));

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

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementContractOperationsNotFoundException exception)
        {
            return NotFound(Problem(
                404, exception.Code,
                "Contract operations record not found",
                exception.Message));
        }
        catch (ProcurementContractOperationsAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                403, "CONTRACT_OPERATIONS_ACCESS_FORBIDDEN",
                "Contract operations access forbidden",
                exception.Message));
        }
        catch (ProcurementContractOperationsValidationException exception)
        {
            return UnprocessableEntity(Problem(
                422, exception.Code,
                "Contract operations validation failed",
                exception.Message));
        }
    }

    private static ProblemDetails Problem(
        int status,
        string code,
        string title,
        string detail) => new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code }
        };
}
