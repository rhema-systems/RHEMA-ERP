using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/framework-call-offs")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementFrameworkCallOffsController : ControllerBase
{
    private readonly IProcurementFrameworkCallOffService _service;

    public ProcurementFrameworkCallOffsController(
        IProcurementFrameworkCallOffService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementFrameworkCallOffSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetOptionsAsync(cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateProcurementFrameworkCallOffRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var callOff = await _service.CreateAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = callOff.Id }, callOff);
        });

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementFrameworkCallOffLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.SubmitAsync(
                id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/decision")]
    public Task<IActionResult> Decide(
        Guid id,
        [FromBody] ProcurementFrameworkCallOffDecisionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.DecideAsync(
                id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/issue")]
    public Task<IActionResult> Issue(
        Guid id,
        [FromBody] ProcurementFrameworkCallOffLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.IssueAsync(
                id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(
        Guid id,
        [FromBody] ProcurementFrameworkCallOffLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.CancelAsync(
                id, request, CorrelationId, cancellationToken)));

    [HttpPost("process-expiry-alerts")]
    public Task<IActionResult> ProcessExpiryAlerts(
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(new
        {
            alerted = await _service.ProcessExpiryAlertsAsync(cancellationToken)
        }));

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
        catch (ProcurementFrameworkCallOffNotFoundException exception)
        {
            return NotFound(Problem(
                404, exception.Code, "Framework call-off not found", exception.Message));
        }
        catch (ProcurementFrameworkCallOffAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                403, "FRAMEWORK_CALL_OFF_ACCESS_FORBIDDEN",
                "Framework call-off access forbidden", exception.Message));
        }
        catch (ProcurementFrameworkCallOffConflictException exception)
        {
            return Conflict(Problem(
                409, exception.Code, "Framework call-off conflict", exception.Message));
        }
        catch (ProcurementFrameworkCallOffValidationException exception)
        {
            return ValidationProblem(exception.Code, exception.Message);
        }
        catch (SupplierEligibilityException exception)
        {
            return ValidationProblem(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(
                409,
                "FRAMEWORK_CALL_OFF_CONCURRENCY_CONFLICT",
                "Framework call-off conflict",
                "The call-off or agreement balance changed; reload before retrying."));
        }
    }

    private UnprocessableEntityObjectResult ValidationProblem(
        string code,
        string detail)
    {
        var problem = new ValidationProblemDetails(
            new Dictionary<string, string[]> { [code] = [detail] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Framework call-off validation failed",
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationId;
        return UnprocessableEntity(problem);
    }

    private ProblemDetails Problem(
        int status,
        string code,
        string title,
        string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = CorrelationId
        }
    };
}
