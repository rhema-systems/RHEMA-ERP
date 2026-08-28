using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-due-diligence")]
[Authorize]
public sealed class ProcurementSupplierDueDiligenceController : ControllerBase
{
    private readonly IProcurementSupplierDueDiligenceService _service;

    public ProcurementSupplierDueDiligenceController(
        IProcurementSupplierDueDiligenceService service) =>
        _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("workflow-options")]
    public Task<IActionResult> WorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowOptionsAsync(cancellationToken)));

    [HttpGet("supplier-options")]
    public Task<IActionResult> SupplierOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSupplierOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementSupplierDueDiligenceSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("suppliers/{businessPartnerId:guid}/current")]
    public Task<IActionResult> CurrentState(
        Guid businessPartnerId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCurrentStateAsync(
            businessPartnerId, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateProcurementSupplierDueDiligenceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProcurementSupplierDueDiligenceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementSupplierDueDiligenceLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(
        Guid id,
        [FromBody] ProcurementSupplierDueDiligenceLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ApproveAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/supersede-stale")]
    public Task<IActionResult> SupersedeStale(
        Guid id,
        [FromBody] ProcurementSupplierDueDiligenceLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SupersedeStaleAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] ProcurementSupplierDueDiligenceLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("process-expiry")]
    public Task<IActionResult> ProcessExpiry(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(new
        {
            processed = await _service.ProcessExpiryAsync(cancellationToken: cancellationToken)
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
        try { return await action(); }
        catch (ProcurementSupplierDueDiligenceNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier due-diligence record not found", exception.Message));
        }
        catch (ProcurementSupplierDueDiligenceAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_DUE_DILIGENCE_ACCESS_FORBIDDEN",
                    "Supplier due-diligence access forbidden", exception.Message));
        }
        catch (ProcurementSupplierDueDiligenceConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier due-diligence conflict", exception.Message));
        }
        catch (ProcurementSupplierDueDiligenceValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier due-diligence validation failed",
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
