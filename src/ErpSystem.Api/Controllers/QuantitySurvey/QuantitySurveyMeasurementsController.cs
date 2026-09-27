using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/measurements")]
public sealed class QuantitySurveyMeasurementsController(IQuantitySurveyMeasurementService service) : ControllerBase
{
    [HttpGet("search"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Search([FromQuery] string search, CancellationToken token, [FromQuery] int take = 8) =>
        ExecuteAsync(async () => Ok(await service.SearchAsync(search, take, token)));
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyMeasurementListRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(request, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPut("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateQuantitySurveyMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.UpdateAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/record"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Record(Guid id, [FromBody] RecordQuantitySurveyMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RecordAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/attachments"), RequestSizeLimit(11 * 1024 * 1024),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> AddAttachment(Guid id, [FromForm] IFormFile file, [FromForm] Guid clientRequestId,
        [FromForm] QuantitySurveyMeasurementEvidenceType evidenceType, [FromForm] string title, CancellationToken token) =>
        ExecuteAsync(async () =>
        {
            if (file is null) return BadRequest(Problem(400, "QS measurement validation failed", "Select an evidence file."));
            await using var stream = file.OpenReadStream();
            return Ok(await service.AddAttachmentAsync(id, stream, file.FileName, file.ContentType,
                new AddQuantitySurveyMeasurementAttachmentRequest
                {
                    ClientRequestId = clientRequestId, EvidenceType = evidenceType, Title = title
                }, CorrelationId, token));
        });

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content"),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> OpenAttachment(Guid id, Guid attachmentId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenAttachmentAsync(id, attachmentId, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyMeasurementNotFoundException exception)
        { return NotFound(Problem(404, "QS measurement not found", exception.Message)); }
        catch (QuantitySurveyMeasurementConflictException exception)
        { return Conflict(Problem(409, "QS measurement conflict", exception.Message)); }
        catch (QuantitySurveyMeasurementValidationException exception)
        { return BadRequest(Problem(400, "QS measurement validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS measurement evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "QS measurement access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-measurement-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_MEASUREMENT_{status}", ["correlationId"] = CorrelationId }
    };
}
