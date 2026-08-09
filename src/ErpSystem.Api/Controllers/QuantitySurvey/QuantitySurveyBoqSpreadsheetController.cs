using ErpSystem.Api.Middleware;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/boq-spreadsheet")]
public sealed class QuantitySurveyBoqSpreadsheetController(
    IQuantitySurveyBoqSpreadsheetService spreadsheets) : ControllerBase
{
    [HttpGet("template")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public Task<IActionResult> Template(Guid projectId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => FileResult(await spreadsheets.CreateTemplateAsync(projectId, cancellationToken)));

    [HttpGet("export")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Export(Guid projectId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => FileResult(await spreadsheets.ExportAsync(projectId, cancellationToken)));

    [HttpPost("preview")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    [RequestSizeLimit(15_728_640)]
    public Task<IActionResult> Preview(
        Guid projectId,
        IFormFile file,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        if (file == null || file.Length == 0)
            return BadRequest(Problem(400, "BoQ workbook required", "Select a non-empty .xlsx BoQ workbook."));
        await using var stream = file.OpenReadStream();
        return Ok(await spreadsheets.PreviewAsync(
            projectId,
            stream,
            Path.GetFileName(file.FileName),
            file.ContentType,
            cancellationToken));
    });

    [HttpPost("sessions/{sessionId:guid}/commit")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public Task<IActionResult> Commit(
        Guid projectId,
        Guid sessionId,
        [FromBody] CommitQuantitySurveyBoqImportDto request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await spreadsheets.CommitAsync(projectId, sessionId, request, cancellationToken)));

    [HttpGet("sessions/{sessionId:guid}/validation-report")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public Task<IActionResult> ValidationReport(
        Guid projectId,
        Guid sessionId,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            FileResult(await spreadsheets.CreateErrorWorkbookAsync(projectId, sessionId, cancellationToken)));

    private FileContentResult FileResult(QuantitySurveyBoqFileDto file) =>
        File(file.Content, file.ContentType, file.FileName);

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ControlledFileUploadException exception)
        {
            return StatusCode(exception.StatusCode, Problem(
                exception.StatusCode,
                "BoQ workbook upload failed",
                exception.Message,
                exception.Code));
        }
        catch (ConflictException exception)
        {
            return Conflict(Problem(409, "BoQ import conflict", exception.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(Problem(400, "BoQ spreadsheet validation failed", exception.Message));
        }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-boq-spreadsheet-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code ?? $"QS_BOQ_SPREADSHEET_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
