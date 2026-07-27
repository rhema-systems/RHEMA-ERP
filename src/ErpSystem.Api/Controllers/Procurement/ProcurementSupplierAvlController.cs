using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-avl")]
[Authorize]
public sealed class ProcurementSupplierAvlController : ControllerBase
{
    private readonly IProcurementSupplierAvlService _service;

    public ProcurementSupplierAvlController(IProcurementSupplierAvlService service) =>
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
        [FromQuery] ProcurementSupplierAvlSearchRequest request,
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

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateProcurementSupplierAvlRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProcurementSupplierAvlRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/entries")]
    public Task<IActionResult> AddEntry(
        Guid id,
        [FromBody] AddProcurementSupplierAvlEntryRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.AddEntryAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    public Task<IActionResult> RemoveEntry(
        Guid id,
        Guid entryId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RemoveEntryAsync(
            id, entryId, rowVersion, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementSupplierAvlLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(
        Guid id,
        [FromBody] ProcurementSupplierAvlLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ApproveAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] ProcurementSupplierAvlLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(
        Guid id,
        [FromBody] ProcurementSupplierAvlLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.PublishAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/entries/{entryId:guid}/suspend")]
    public Task<IActionResult> SuspendEntry(
        Guid id,
        Guid entryId,
        [FromBody] ProcurementSupplierAvlEntryLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SuspendEntryAsync(
            id, entryId, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/entries/{entryId:guid}/reinstate")]
    public Task<IActionResult> ReinstateEntry(
        Guid id,
        Guid entryId,
        [FromBody] ProcurementSupplierAvlEntryLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ReinstateEntryAsync(
            id, entryId, request, CorrelationId, cancellationToken)));

    [HttpPost("process-expiry")]
    public Task<IActionResult> ProcessExpiry(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(new
        {
            processed = await _service.ProcessExpiryAsync(cancellationToken: cancellationToken)
        }));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(Request.Headers["X-Correlation-ID"].FirstOrDefault())
            ? HttpContext.TraceIdentifier
            : Request.Headers["X-Correlation-ID"].First()!;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementSupplierAvlNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier AVL record not found", exception.Message));
        }
        catch (ProcurementSupplierAvlAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_AVL_ACCESS_FORBIDDEN",
                    "Supplier AVL access forbidden", exception.Message));
        }
        catch (ProcurementSupplierAvlConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier AVL conflict", exception.Message));
        }
        catch (ProcurementSupplierAvlValidationException exception)
        {
            var problem = new ValidationProblemDetails(
                new Dictionary<string, string[]> { [exception.Code] = [exception.Message] })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier AVL validation failed",
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
