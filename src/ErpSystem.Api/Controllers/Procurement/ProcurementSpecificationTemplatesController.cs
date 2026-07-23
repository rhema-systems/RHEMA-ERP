using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/specification-templates")]
[Authorize]
public sealed class ProcurementSpecificationTemplatesController : ControllerBase
{
    private readonly IProcurementSpecificationTemplateService _service;

    public ProcurementSpecificationTemplatesController(IProcurementSpecificationTemplateService service) => _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("workflow-options")]
    public Task<IActionResult> GetWorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowOptionsAsync(cancellationToken)));

    [HttpGet("effective")]
    public Task<IActionResult> GetEffective([FromQuery] string templateCode, [FromQuery] DateTime? atUtc,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var value = await _service.GetEffectiveAsync(templateCode, atUtc ?? DateTime.UtcNow, cancellationToken);
        return value is null ? NotFound(Problem(404, "Specification template not found",
            "No published specification template is effective for the supplied code and date.")) : Ok(value);
    });

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ProcurementSpecificationTemplateSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("{id:guid}/validation")]
    public Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ValidateAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveProcurementSpecificationTemplateRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var value = await _service.CreateAsync(request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveProcurementSpecificationTemplateRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, [FromBody] ProcurementSpecificationTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, [FromBody] ProcurementSpecificationTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.PublishAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] ProcurementSpecificationTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone")]
    public Task<IActionResult> Clone(Guid id, [FromBody] CloneProcurementSpecificationTemplateRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var value = await _service.CloneAsync(id, request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    });

    [HttpPost("{id:guid}/retire")]
    public Task<IActionResult> Retire(Guid id, [FromBody] ProcurementSpecificationTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetireAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/delete-draft")]
    public Task<IActionResult> DeleteDraft(Guid id, [FromBody] ProcurementSpecificationTemplateLifecycleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        await _service.DeleteDraftAsync(id, request, CorrelationId, cancellationToken);
        return NoContent();
    });

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? (string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier)
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementSpecificationTemplateNotFoundException exception)
        {
            return NotFound(Problem(404, "Specification template not found", exception.Message));
        }
        catch (ProcurementSpecificationTemplateAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Specification template access forbidden", exception.Message));
        }
        catch (ProcurementSpecificationTemplateConflictException exception)
        {
            return Conflict(Problem(409, "Specification template conflict", exception.Message));
        }
        catch (ProcurementSpecificationTemplateValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Specification template validation failed",
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
