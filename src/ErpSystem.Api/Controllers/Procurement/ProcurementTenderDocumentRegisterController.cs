using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/tender-document-register")]
[Authorize]
public sealed class ProcurementTenderDocumentRegisterController : ControllerBase
{
    private readonly IProcurementTenderDocumentControlService _service;

    public ProcurementTenderDocumentRegisterController(
        IProcurementTenderDocumentControlService service) =>
        _service = service;

    [HttpGet("readiness")]
    public Task<IActionResult> GetReadiness(
        [FromQuery] ProcurementTenderDocumentSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetRegisterReadinessAsync(sourceType, sourceId, cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Get(
        [FromQuery] ProcurementTenderDocumentSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetRegisterAsync(sourceType, sourceId, cancellationToken)));

    [HttpPost("bind")]
    public Task<IActionResult> Bind(
        [FromBody] BindProcurementTenderDocumentRegisterRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.BindAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get),
                new { sourceType = value.SourceType, sourceId = value.SourceId }, value);
        });

    [HttpPost("issue")]
    public Task<IActionResult> Issue(
        [FromBody] IssueProcurementTenderDocumentControlRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.IssueAsync(request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("changes")]
    public Task<IActionResult> CreateChange(
        [FromBody] CreateProcurementTenderDocumentChangeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateChangeAsync(request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("changes/{changeId:guid}/decision")]
    public Task<IActionResult> DecideChange(
        Guid changeId,
        [FromBody] DecideProcurementTenderDocumentChangeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.DecideChangeAsync(changeId, request, CorrelationId, cancellationToken)));

    [HttpPost("acknowledgements")]
    public Task<IActionResult> Acknowledge(
        [FromBody] AcknowledgeProcurementTenderDocumentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.AcknowledgeAsync(request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
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
