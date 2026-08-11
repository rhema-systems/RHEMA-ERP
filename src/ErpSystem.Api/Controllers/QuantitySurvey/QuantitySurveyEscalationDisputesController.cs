using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/escalation-disputes")]
public sealed class QuantitySurveyEscalationDisputesController(
    IQuantitySurveyEscalationDisputeService service,
    IDocumentOutputService documentOutput) : ControllerBase
{
    [HttpGet("calculation-lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> CalculationLookups(CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetCalculationLookupsAsync(token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyEscalationDisputeListRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(request, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> Open([FromBody] CreateQuantitySurveyEscalationDisputeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.OpenAsync(request, CorrelationId, token)));

    [HttpPost("{id:guid}/contractor-response"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> Respond(Guid id, [FromBody] RespondQuantitySurveyEscalationDisputeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RespondAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/resolve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Resolve(Guid id, [FromBody] ResolveQuantitySurveyEscalationDisputeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ResolveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/attachments"), RequestSizeLimit(11 * 1024 * 1024),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> AddAttachment(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] Guid clientRequestId,
        [FromForm] QuantitySurveyEscalationDisputeAttachmentType attachmentType,
        [FromForm] string title,
        CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS escalation dispute validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddAttachmentAsync(id, stream, file.FileName, file.ContentType,
            new AddQuantitySurveyEscalationDisputeAttachmentRequest
            {
                ClientRequestId = clientRequestId, AttachmentType = attachmentType, Title = title
            }, CorrelationId, token));
    });

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content"),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> OpenAttachment(Guid id, Guid attachmentId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenAttachmentAsync(id, attachmentId, token);
        await using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    [HttpGet("{id:guid}/audit-pack"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> AuditPack(Guid id, [FromQuery] string format = "pdf", CancellationToken token = default) =>
        ExecuteAsync(async () =>
        {
            if (format is not ("pdf" or "zip"))
                return BadRequest(Problem(400, "QS escalation dispute export failed", "Select PDF or ZIP export format."));
            var result = await documentOutput.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = DocumentTypes.QuantitySurveyEscalationDisputeAuditPack,
                EntityId = id,
                Format = format,
                Options = new Dictionary<string, string> { ["correlationId"] = CorrelationId }
            }, token);
            return File(result.Content, result.ContentType, result.FileName);
        });

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyEscalationDisputeNotFoundException exception)
        { return NotFound(Problem(404, "QS escalation dispute not found", exception.Message)); }
        catch (QuantitySurveyEscalationDisputeConflictException exception)
        { return Conflict(Problem(409, "QS escalation dispute conflict", exception.Message)); }
        catch (QuantitySurveyEscalationDisputeValidationException exception)
        { return BadRequest(Problem(400, "QS escalation dispute validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS escalation evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "QS escalation dispute access forbidden", exception.Message)); }
        catch (NotSupportedException exception)
        { return BadRequest(Problem(400, "QS escalation dispute export failed", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-escalation-dispute-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code ?? $"QS_ESCALATION_DISPUTE_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}
