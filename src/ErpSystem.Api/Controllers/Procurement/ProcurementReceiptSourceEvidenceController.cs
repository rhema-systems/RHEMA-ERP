using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/purchase-order-receipts/{receiptId:guid}/source-evidence")]
[Authorize]
public sealed class ProcurementReceiptSourceEvidenceController(
    IProcurementReceiptSourceEvidenceService service,
    ILogger<ProcurementReceiptSourceEvidenceController> logger) : ControllerBase
{
    private const long MaximumFileBytes = 25_000_000;

    [HttpGet]
    public Task<ActionResult<ProcurementReceiptSourceEvidenceOverviewDto>> Get(Guid receiptId) =>
        ExecuteAsync(() => service.GetOverviewAsync(receiptId, HttpContext.RequestAborted));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileBytes + 100_000)]
    public async Task<ActionResult<ProcurementReceiptSourceEvidenceDto>> Upload(
        Guid receiptId,
        [FromForm] UploadProcurementReceiptSourceEvidenceRequest request,
        [FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return UnprocessableEntity(Problem("RCV_SOURCE_EVIDENCE_FILE_REQUIRED",
                "Select a receipt-evidence file."));
        if (file.Length > MaximumFileBytes)
            return UnprocessableEntity(Problem("RCV_SOURCE_EVIDENCE_FILE_TOO_LARGE",
                "Receipt-evidence files cannot exceed 25 MB."));
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, HttpContext.RequestAborted);
        return await ExecuteAsync(() => service.UploadAsync(receiptId,
            new ProcurementReceiptSourceEvidenceUploadCommand
            {
                EvidenceKind = request.EvidenceKind,
                ReferenceNumber = request.ReferenceNumber,
                DocumentDate = request.DocumentDate,
                ClientRequestId = request.ClientRequestId,
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream" : file.ContentType,
                Content = buffer.ToArray()
            }, HttpContext.TraceIdentifier, HttpContext.RequestAborted));
    }

    [HttpGet("{evidenceId:guid}/download")]
    public async Task<IActionResult> Download(Guid receiptId, Guid evidenceId)
    {
        try
        {
            await using var content = await service.OpenAsync(receiptId, evidenceId,
                HttpContext.RequestAborted);
            return File(content.Content, content.ContentType, content.FileName);
        }
        catch (ProcurementReceiptSourceEvidenceNotFoundException exception)
        { return NotFound(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, Problem("RCV_SOURCE_EVIDENCE_FORBIDDEN", exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceValidationException exception)
        { return UnprocessableEntity(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceConflictException exception)
        { return Conflict(Problem(exception.Code, exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.Code, exception.Message)); }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ProcurementReceiptSourceEvidenceNotFoundException exception)
        { return NotFound(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, Problem("RCV_SOURCE_EVIDENCE_FORBIDDEN", exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceValidationException exception)
        { return UnprocessableEntity(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptSourceEvidenceConflictException exception)
        { return Conflict(Problem(exception.Code, exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.Code, exception.Message)); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected receipt-source evidence failure for receipt {ReceiptId}",
                RouteData.Values["receiptId"]);
            throw;
        }
    }

    private static ProblemDetails Problem(string code, string message)
    {
        var details = new ProblemDetails
        {
            Title = code,
            Detail = message
        };
        details.Extensions["code"] = code;
        return details;
    }
}
