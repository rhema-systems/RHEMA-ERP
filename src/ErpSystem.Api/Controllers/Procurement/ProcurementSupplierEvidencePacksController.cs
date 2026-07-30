using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-evidence-packs")]
[Authorize]
public sealed class ProcurementSupplierEvidencePacksController : ControllerBase
{
    private readonly IProcurementSupplierEvidencePackService _service;

    public ProcurementSupplierEvidencePacksController(
        IProcurementSupplierEvidencePackService service) =>
        _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("workflow-options")]
    public Task<IActionResult> WorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowOptionsAsync(cancellationToken)));

    [HttpGet("configuration-profile-options")]
    public Task<IActionResult> ConfigurationProfileOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetConfigurationProfileOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementSupplierEvidencePackSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] SaveProcurementSupplierEvidencePackRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] SaveProcurementSupplierEvidencePackRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementSupplierEvidencePackLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(
        Guid id,
        [FromBody] ProcurementSupplierEvidencePackLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.PublishAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] ProcurementSupplierEvidencePackLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone")]
    public Task<IActionResult> Clone(
        Guid id,
        [FromBody] CloneProcurementSupplierEvidencePackRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CloneAsync(id, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPost("{id:guid}/retire")]
    public Task<IActionResult> Retire(
        Guid id,
        [FromBody] ProcurementSupplierEvidencePackLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetireAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/delete-draft")]
    public Task<IActionResult> DeleteDraft(
        Guid id,
        [FromBody] ProcurementSupplierEvidencePackLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _service.DeleteDraftAsync(id, request, CorrelationId, cancellationToken);
            return NoContent();
        });

    [HttpGet("registrations/{registrationId:guid}/readiness")]
    public Task<IActionResult> RegistrationReadiness(
        Guid registrationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetRegistrationReadinessAsync(
            registrationId, cancellationToken)));

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
        catch (ProcurementSupplierEvidencePackNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier evidence-pack record not found", exception.Message));
        }
        catch (ProcurementSupplierEvidencePackAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_EVIDENCE_PACK_ACCESS_FORBIDDEN",
                    "Supplier evidence-pack access forbidden", exception.Message));
        }
        catch (ProcurementSupplierEvidencePackConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier evidence-pack conflict", exception.Message));
        }
        catch (ProcurementSupplierEvidencePackValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier evidence-pack validation failed",
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
