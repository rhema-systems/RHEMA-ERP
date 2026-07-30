using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/tender-document-templates")]
[Authorize]
public sealed class ProcurementTenderDocumentTemplatesController : ControllerBase
{
    private readonly IProcurementTenderDocumentControlService _service;

    public ProcurementTenderDocumentTemplatesController(
        IProcurementTenderDocumentControlService service) =>
        _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetTemplateSummaryAsync(cancellationToken)));

    [HttpGet("workflow-options")]
    public Task<IActionResult> GetWorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetTemplateWorkflowOptionsAsync(cancellationToken)));

    [HttpGet("policy-options")]
    public Task<IActionResult> GetPolicyOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetTemplatePolicyOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementTenderDocumentTemplateSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchTemplatesAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetTemplateAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] SaveProcurementTenderDocumentTemplateRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateTemplateAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] SaveProcurementTenderDocumentTemplateRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.UpdateTemplateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementTenderDocumentTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.SubmitTemplateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(
        Guid id,
        [FromBody] ProcurementTenderDocumentTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.PublishTemplateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] ProcurementTenderDocumentTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.RejectTemplateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone")]
    public Task<IActionResult> Clone(
        Guid id,
        [FromBody] CloneProcurementTenderDocumentTemplateRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CloneTemplateAsync(id, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPost("{id:guid}/retire")]
    public Task<IActionResult> Retire(
        Guid id,
        [FromBody] ProcurementTenderDocumentTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.RetireTemplateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/delete-draft")]
    public Task<IActionResult> DeleteDraft(
        Guid id,
        [FromBody] ProcurementTenderDocumentTemplateLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _service.DeleteDraftTemplateAsync(id, request, CorrelationId, cancellationToken);
            return NoContent();
        });

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
        catch (ProcurementTenderDocumentControlNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Tender-document control record not found", exception.Message));
        }
        catch (ProcurementTenderDocumentControlAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "TENDER_DOCUMENT_ACCESS_FORBIDDEN",
                    "Tender-document control access forbidden", exception.Message));
        }
        catch (ProcurementTenderDocumentControlConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Tender-document control conflict", exception.Message));
        }
        catch (ProcurementTenderDocumentControlValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Tender-document control validation failed",
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
