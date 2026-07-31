using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/ProcurementReceiptDocuments")]
[Authorize]
public sealed class ProcurementReceiptDocumentsController : ControllerBase
{
    private readonly IProcurementReceiptDocumentService _service;
    private readonly ILogger<ProcurementReceiptDocumentsController> _logger;

    public ProcurementReceiptDocumentsController(
        IProcurementReceiptDocumentService service,
        ILogger<ProcurementReceiptDocumentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("receipt/{receiptId:guid}")]
    public Task<ActionResult<ProcurementReceiptDocumentOverviewDto>> GetOverview(Guid receiptId) =>
        ExecuteAsync(() => _service.GetOverviewAsync(receiptId, HttpContext.RequestAborted));

    [HttpPost("receipt/{receiptId:guid}/ensure")]
    public Task<ActionResult<ProcurementReceiptDocumentOverviewDto>> Ensure(Guid receiptId) =>
        ExecuteAsync(() => _service.EnsureAsync(receiptId, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("receipt/{receiptId:guid}/reconcile")]
    public Task<ActionResult<ProcurementReceiptDocumentOverviewDto>> Reconcile(Guid receiptId) =>
        ExecuteAsync(() => _service.ReconcileAsync(receiptId, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("{documentId:guid}/sign")]
    public Task<ActionResult<ProcurementReceiptDocumentDto>> Sign(
        Guid documentId,
        [FromBody] SignProcurementReceiptDocumentRequest request) =>
        ExecuteAsync(() => _service.SignAsync(documentId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("{documentId:guid}/issue")]
    public Task<ActionResult<ProcurementReceiptDocumentDto>> Issue(
        Guid documentId,
        [FromBody] IssueProcurementReceiptDocumentRequest request) =>
        ExecuteAsync(() => _service.IssueAsync(documentId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("{documentId:guid}/cancel")]
    public Task<ActionResult<ProcurementReceiptDocumentDto>> Cancel(
        Guid documentId,
        [FromBody] CancelProcurementReceiptDocumentRequest request) =>
        ExecuteAsync(() => _service.CancelAsync(documentId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId)
    {
        try
        {
            var file = await _service.DownloadAsync(documentId, HttpContext.RequestAborted);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ProcurementReceiptDocumentNotFoundException exception) { return NotFound(Problem("RCV_DOCUMENT_NOT_FOUND", exception.Message)); }
        catch (ProcurementReceiptDocumentAuthorizationException exception) { return StatusCode(StatusCodes.Status403Forbidden, Problem("RCV_DOCUMENT_FORBIDDEN", exception.Message)); }
        catch (ProcurementReceiptDocumentValidationException exception) { return UnprocessableEntity(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptDocumentConflictException exception) { return Conflict(Problem(exception.Code, exception.Message)); }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ProcurementReceiptDocumentNotFoundException exception) { return NotFound(Problem("RCV_DOCUMENT_NOT_FOUND", exception.Message)); }
        catch (ProcurementReceiptDocumentAuthorizationException exception) { return StatusCode(StatusCodes.Status403Forbidden, Problem("RCV_DOCUMENT_FORBIDDEN", exception.Message)); }
        catch (ProcurementReceiptDocumentValidationException exception) { return UnprocessableEntity(Problem(exception.Code, exception.Message)); }
        catch (ProcurementReceiptDocumentConflictException exception) { return Conflict(Problem(exception.Code, exception.Message)); }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "Receipt-document concurrency conflict");
            return Conflict(Problem("RCV_DOCUMENT_ROW_VERSION_CONFLICT", "The receipt document changed. Reload and retry."));
        }
    }

    private static object Problem(string code, string message) => new { code, message };
}
