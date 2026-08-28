using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/dayworks")]
public sealed class QuantitySurveyDayworksController(IQuantitySurveyDayworkService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, false, token)));

    [HttpPost("{id:guid}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid id, [FromForm] Guid clientRequestId, [FromForm] string title,
        [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS daywork validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName, file.ContentType,
            file.Length, () => stream, false, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/verify"), Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public Task<IActionResult> Verify(Guid id, [FromBody] QuantitySurveyDayworkActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.VerifyAsync(id, request, true, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyDayworkActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.VerifyAsync(id, request, false, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyDayworkNotFoundException exception) { return NotFound(Problem(404, "QS daywork not found", exception.Message)); }
        catch (QuantitySurveyDayworkConflictException exception) { return Conflict(Problem(409, "QS daywork conflict", exception.Message)); }
        catch (QuantitySurveyDayworkValidationException exception) { return BadRequest(Problem(400, "QS daywork validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS daywork evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS daywork access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    { Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-daywork-{status}",
      Instance = HttpContext.Request.Path, Extensions = { ["code"] = code ?? $"QS_DAYWORK_{status}", ["correlationId"] = CorrelationId } };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/dayworks")]
public sealed class ExternalProjectDayworksController(IQuantitySurveyDayworkService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Workspace(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, true, token)));

    [HttpPut]
    public Task<IActionResult> Save(Guid projectId, [FromBody] SaveQuantitySurveyDayworkRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveExternalAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid projectId, Guid id, [FromForm] Guid clientRequestId,
        [FromForm] string title, [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS daywork validation failed", "Select an evidence file."));
        var workspace = await service.GetWorkspaceAsync(projectId, true, token);
        if (workspace.Sheets.All(value => value.Id != id)) return NotFound(Problem(404, "QS daywork not found", "The sheet was not found in this project."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName, file.ContentType,
            file.Length, () => stream, true, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        var workspace = await service.GetWorkspaceAsync(projectId, true, token);
        if (workspace.Sheets.All(value => value.Id != id)) return NotFound(Problem(404, "QS daywork not found", "The sheet was not found in this project."));
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/sign")]
    public Task<IActionResult> Sign(Guid projectId, Guid id, [FromBody] QuantitySurveyDayworkActionRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SignExternalAsync(id, request, CorrelationId, token), token);

    private async Task<IActionResult> ExecuteForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveyDayworkSheetDto>> action, CancellationToken token)
    {
        return await ExecuteAsync(async () =>
        {
            var workspace = await service.GetWorkspaceAsync(projectId, true, token);
            if (workspace.Sheets.All(value => value.Id != id)) return NotFound(Problem(404, "QS daywork not found", "The sheet was not found in this project."));
            return Ok(await action());
        });
    }

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyDayworkNotFoundException exception) { return NotFound(Problem(404, "QS daywork not found", exception.Message)); }
        catch (QuantitySurveyDayworkConflictException exception) { return Conflict(Problem(409, "QS daywork conflict", exception.Message)); }
        catch (QuantitySurveyDayworkValidationException exception) { return BadRequest(Problem(400, "QS daywork validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS daywork evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS daywork access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    { Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-external-daywork-{status}",
      Instance = HttpContext.Request.Path, Extensions = { ["code"] = code ?? $"QS_EXTERNAL_DAYWORK_{status}", ["correlationId"] = CorrelationId } };
}
