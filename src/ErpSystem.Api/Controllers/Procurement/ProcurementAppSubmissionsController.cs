using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/app-submissions")]
[Authorize]
public sealed class ProcurementAppSubmissionsController : ControllerBase
{
    private readonly IProcurementAppSubmissionService _service;

    public ProcurementAppSubmissionsController(IProcurementAppSubmissionService service) => _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("published-plans")]
    public Task<IActionResult> GetPublishedPlans(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPublishedPlanOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ProcurementAppSubmissionSearchRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpPost("exports")]
    public Task<IActionResult> RecordExport([FromBody] RecordProcurementAppExportRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.RecordExportAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, [FromBody] SubmitProcurementAppRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/acknowledge")]
    public Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeProcurementAppRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.AcknowledgeAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectProcurementAppRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/resubmit")]
    public Task<IActionResult> Resubmit(Guid id, [FromBody] ResubmitProcurementAppRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ResubmitAsync(id, request, CorrelationId, cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? (string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier) ? Guid.NewGuid().ToString("N") : HttpContext.TraceIdentifier)
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementAppSubmissionNotFoundException exception)
        {
            return NotFound(Problem(404, "APP submission record not found", exception.Message));
        }
        catch (ProcurementAppSubmissionAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "APP submission access forbidden", exception.Message));
        }
        catch (ProcurementAppSubmissionConflictException exception)
        {
            return Conflict(Problem(409, "APP submission conflict", exception.Message));
        }
        catch (ProcurementAppSubmissionValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { [exception.Code] = [exception.Message] })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "APP submission validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}
