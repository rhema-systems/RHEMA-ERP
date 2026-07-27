using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-risk")]
[Authorize]
public sealed class ProcurementSupplierRiskController : ControllerBase
{
    private readonly IProcurementSupplierRiskService _service;

    public ProcurementSupplierRiskController(IProcurementSupplierRiskService service) =>
        _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("supplier-options")]
    public Task<IActionResult> SupplierOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSupplierOptionsAsync(cancellationToken)));

    [HttpGet("workflow-options")]
    public Task<IActionResult> WorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementSupplierRiskSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("suppliers/{businessPartnerId:guid}/current")]
    public Task<IActionResult> Current(
        Guid businessPartnerId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCurrentStateAsync(
            businessPartnerId, cancellationToken)));

    [HttpPost("assessments")]
    public Task<IActionResult> Evaluate(
        [FromBody] EvaluateProcurementSupplierRiskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await _service.EvaluateAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });

    [HttpPost("alerts/{alertId:guid}/escalate")]
    public Task<IActionResult> Escalate(
        Guid alertId,
        [FromBody] EscalateProcurementSupplierRiskAlertRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.EscalateAsync(
            alertId, request, CorrelationId, cancellationToken)));

    [HttpPost("alerts/{alertId:guid}/resolve")]
    public Task<IActionResult> Resolve(
        Guid alertId,
        [FromBody] ResolveProcurementSupplierRiskAlertRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ResolveAsync(
            alertId, request, CorrelationId, cancellationToken)));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(Request.Headers["X-Correlation-ID"].FirstOrDefault())
            ? HttpContext.TraceIdentifier
            : Request.Headers["X-Correlation-ID"].First()!;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementSupplierRiskNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier risk record not found", exception.Message));
        }
        catch (ProcurementSupplierRiskAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_RISK_ACCESS_FORBIDDEN",
                    "Supplier risk access forbidden", exception.Message));
        }
        catch (ProcurementSupplierRiskConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier risk conflict", exception.Message));
        }
        catch (ProcurementSupplierRiskValidationException exception)
        {
            var problem = new ValidationProblemDetails(
                new Dictionary<string, string[]> { [exception.Code] = [exception.Message] })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier risk validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string code, string title, string detail) => new()
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
