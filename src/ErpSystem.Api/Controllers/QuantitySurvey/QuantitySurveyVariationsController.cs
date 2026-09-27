using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/variations")]
public sealed class QuantitySurveyVariationsController(IQuantitySurveyVariationService service) : ControllerBase
{
    [HttpGet("search"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Search([FromQuery] string search, CancellationToken token, [FromQuery] int take = 8) =>
        ExecuteAsync(async () => Ok(await service.SearchAsync(search, take, token)));
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPut, Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public Task<IActionResult> Save([FromQuery] Guid projectId, [FromBody] SaveQuantitySurveyVariationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid id, [FromForm] Guid clientRequestId, [FromForm] string title, IFormFile file, CancellationToken token) =>
        ExecuteAsync(async () => { await using var stream = file.OpenReadStream(); return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName, file.ContentType, file.Length, () => stream, token)); });

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyVariationActionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyVariationActionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyVariationActionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/apply"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Apply(Guid id, [FromBody] QuantitySurveyVariationActionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ApplyAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyVariationNotFoundException exception) { return NotFound(Problem(404, "QS variation not found", exception.Message)); }
        catch (QuantitySurveyVariationConflictException exception) { return Conflict(Problem(409, "QS variation conflict", exception.Message)); }
        catch (QuantitySurveyVariationValidationException exception) { return BadRequest(Problem(400, "QS variation validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS variation access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-variation-{status}", Instance = HttpContext.Request.Path, Extensions = { ["code"] = $"QS_VARIATION_{status}", ["correlationId"] = CorrelationId } };
}
