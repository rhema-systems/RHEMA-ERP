using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/sourcing-cases")]
[Authorize]
public sealed class ProcurementSourcingCasesController : ControllerBase
{
    private readonly IProcurementSourcingCaseService _service;

    public ProcurementSourcingCasesController(IProcurementSourcingCaseService service) => _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ProcurementSourcingCaseSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("source-options")]
    public Task<IActionResult> GetSourceOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSourceOptionsAsync(cancellationToken)));

    [HttpGet("readiness/{requisitionId:guid}")]
    public Task<IActionResult> GetReadiness(Guid requisitionId, [FromQuery] ProcurementMethodType? method,
        [FromQuery] string? overrideReason,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetReadinessAsync(requisitionId, method, overrideReason, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateProcurementSourcingCaseRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var created = await _service.CreateAsync(request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    });

    [HttpPost("{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, [FromBody] ProcurementSourcingCaseActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CloseAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] ProcurementSourcingCaseActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CancelAsync(id, request, CorrelationId, cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied) ? HttpContext.TraceIdentifier : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementSourcingCaseNotFoundException exception)
        {
            return NotFound(Problem(404, "Procurement sourcing case not found", exception.Message, exception.Code));
        }
        catch (ProcurementSourcingCaseAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Procurement sourcing-case access forbidden", exception.Message, "SOURCING_CASE_FORBIDDEN"));
        }
        catch (ProcurementSourcingCaseConflictException exception)
        {
            return Conflict(Problem(409, "Procurement sourcing-case conflict", exception.Message, exception.Code));
        }
        catch (ProcurementSourcingCaseValidationException exception)
        {
            return UnprocessableEntity(Validation(exception.Code, exception.Message));
        }
        catch (ProcurementComplianceRequestValidationException exception)
        {
            return UnprocessableEntity(Validation(exception.Code, exception.Message));
        }
        catch (ProcurementCompliancePolicyNotFoundException exception)
        {
            return Conflict(Problem(409, "Effective procurement policy missing", exception.Message, "SOURCING_CASE_POLICY_NOT_FOUND"));
        }
        catch (ProcurementCompliancePolicyConflictException exception)
        {
            return Conflict(Problem(409, "Effective procurement policy conflict", exception.Message, "SOURCING_CASE_POLICY_CONFLICT"));
        }
    }

    private ValidationProblemDetails Validation(string code, string message)
    {
        var details = new ValidationProblemDetails(new Dictionary<string, string[]> { [code] = [message] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Procurement sourcing-case validation failed",
            Detail = message,
            Instance = Request.Path
        };
        details.Extensions["code"] = code;
        details.Extensions["correlationId"] = CorrelationId;
        return details;
    }

    private ProblemDetails Problem(int status, string title, string detail, string code) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = CorrelationId }
    };
}
