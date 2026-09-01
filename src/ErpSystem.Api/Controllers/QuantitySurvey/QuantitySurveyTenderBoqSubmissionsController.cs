using ErpSystem.Api.Middleware;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize(Roles = Constants.Roles.ExternalUser)]
[Route("api/external-portal/tender-bids/{tenderBidId:guid}/quantity-survey-boq")]
public sealed class ExternalTenderBoqSubmissionsController(
    IQuantitySurveyTenderBoqSubmissionService submissions) : ControllerBase
{
    [HttpGet("context")]
    public Task<IActionResult> Context(Guid tenderBidId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await submissions.GetExternalContextAsync(tenderBidId, cancellationToken)));

    [HttpGet("template")]
    public Task<IActionResult> Template(Guid tenderBidId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var file = await submissions.CreateExternalTemplateAsync(tenderBidId, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        });

    [HttpPost("preview")]
    [RequestSizeLimit(524_288_000)]
    public Task<IActionResult> Preview(
        Guid tenderBidId,
        IFormFile file,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        if (file == null || file.Length == 0)
            return BadRequest(Problem(400, "Tenderer BoQ workbook required", "Select a non-empty protected .xlsx workbook."));
        await using var stream = file.OpenReadStream();
        return Ok(await submissions.PreviewExternalAsync(
            tenderBidId,
            stream,
            Path.GetFileName(file.FileName),
            file.ContentType,
            HttpContext.TraceIdentifier,
            cancellationToken));
    });

    [HttpPost("submissions/{submissionId:guid}/commit")]
    public Task<IActionResult> Commit(
        Guid tenderBidId,
        Guid submissionId,
        [FromBody] CommitQuantitySurveyTenderBoqSubmissionDto request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await submissions.CommitExternalAsync(
                tenderBidId,
                submissionId,
                request,
                HttpContext.TraceIdentifier,
                cancellationToken)));

    [HttpGet("submissions/latest")]
    public Task<IActionResult> Latest(Guid tenderBidId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await submissions.GetLatestExternalAsync(tenderBidId, cancellationToken);
            return value == null ? NoContent() : Ok(value);
        });

    private Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action) =>
        TenderBoqProblemExecutor.ExecuteAsync(this, action);

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-tender-boq-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_TENDER_BOQ_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}

[ApiController]
[Authorize]
[Route("api/quantity-survey/tender-bids/{tenderBidId:guid}/boq-submissions")]
public sealed class QuantitySurveyTenderBoqSubmissionsController(
    IQuantitySurveyTenderBoqSubmissionService submissions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> History(Guid tenderBidId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await submissions.GetInternalHistoryAsync(tenderBidId, cancellationToken)));

    [HttpPost("{submissionId:guid}/vet")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Vet(
        Guid tenderBidId,
        Guid submissionId,
        [FromBody] VetQuantitySurveyTenderBoqSubmissionDto request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await submissions.VetInternalAsync(
                tenderBidId,
                submissionId,
                request,
                HttpContext.TraceIdentifier,
                cancellationToken)));

    private Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action) =>
        TenderBoqProblemExecutor.ExecuteAsync(this, action);
}

internal static class TenderBoqProblemExecutor
{
    public static async Task<IActionResult> ExecuteAsync(ControllerBase controller, Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ControlledFileUploadException exception)
        {
            return controller.StatusCode(exception.StatusCode,
                Problem(controller, exception.StatusCode, "Tenderer BoQ upload failed", exception.Message, exception.Code));
        }
        catch (KeyNotFoundException exception)
        {
            return controller.NotFound(Problem(controller, 404, "Tenderer BoQ record not found", exception.Message));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return controller.Conflict(Problem(controller, 409, "Tenderer BoQ changed", exception.Message));
        }
        catch (ConflictException exception)
        {
            return controller.Conflict(Problem(controller, 409, "Tenderer BoQ conflict", exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return controller.StatusCode(403,
                Problem(controller, 403, "Tenderer BoQ access forbidden", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return controller.BadRequest(Problem(controller, 400, "Tenderer BoQ validation failed", exception.Message));
        }
    }

    private static ProblemDetails Problem(
        ControllerBase controller,
        int status,
        string title,
        string detail,
        string? code = null) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-tender-boq-{status}",
        Instance = controller.HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code ?? $"QS_TENDER_BOQ_{status}",
            ["correlationId"] = controller.HttpContext.TraceIdentifier
        }
    };
}
