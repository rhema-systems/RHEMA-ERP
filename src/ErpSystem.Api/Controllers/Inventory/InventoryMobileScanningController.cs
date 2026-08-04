using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/mobile-scanning")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryMobileScanningController : ControllerBase
{
    private readonly IInventoryScanningService _service;
    private readonly ILogger<InventoryMobileScanningController> _logger;

    public InventoryMobileScanningController(IInventoryScanningService service, ILogger<InventoryMobileScanningController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("label-profiles")]
    public Task<ActionResult<IReadOnlyList<InventoryLabelProfileDto>>> GetLabelProfiles(CancellationToken cancellationToken) =>
        ExecuteAsync<IReadOnlyList<InventoryLabelProfileDto>>(async () => Ok(await _service.GetLabelProfilesAsync(cancellationToken)), cancellationToken);

    [HttpPost("label-profiles")]
    public Task<ActionResult<InventoryLabelProfileDto>> CreateLabelProfile(
        [FromBody] SaveInventoryLabelProfileRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryLabelProfileDto>(async () => CreatedAtAction(nameof(GetLabelProfiles), await _service.SaveLabelProfileAsync(null, request, CorrelationId, cancellationToken)), cancellationToken);

    [HttpPut("label-profiles/{id:guid}")]
    public Task<ActionResult<InventoryLabelProfileDto>> UpdateLabelProfile(
        Guid id,
        [FromBody] SaveInventoryLabelProfileRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryLabelProfileDto>(async () => Ok(await _service.SaveLabelProfileAsync(id, request, CorrelationId, cancellationToken)), cancellationToken);

    [HttpDelete("label-profiles/{id:guid}")]
    public Task<ActionResult<object?>> DeleteLabelProfile(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<object?>(async () => { await _service.DeleteLabelProfileAsync(id, CorrelationId, cancellationToken); return NoContent(); }, cancellationToken);

    [HttpGet("label-candidates")]
    public Task<ActionResult<IReadOnlyList<InventoryLabelCandidateDto>>> SearchLabelCandidates(
        [FromQuery] string? query,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryLabelCandidateDto>>(async () => Ok(await _service.SearchLabelCandidatesAsync(query, take, cancellationToken)), cancellationToken);

    [HttpPost("label-prints")]
    public Task<ActionResult<InventoryLabelPrintEventDto>> RecordLabelPrint(
        [FromBody] RecordInventoryLabelPrintRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryLabelPrintEventDto>(async () => Ok(await _service.RecordLabelPrintAsync(request, CorrelationId, cancellationToken)), cancellationToken);

    [HttpGet("label-prints")]
    public Task<ActionResult<IReadOnlyList<InventoryLabelPrintEventDto>>> GetRecentPrints(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryLabelPrintEventDto>>(async () => Ok(await _service.GetRecentPrintsAsync(take, cancellationToken)), cancellationToken);

    [HttpGet("documents")]
    public Task<ActionResult<IReadOnlyList<InventoryScanDocumentSummaryDto>>> GetDocuments(
        [FromQuery] InventoryScanOperation operation,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryScanDocumentSummaryDto>>(async () => Ok(await _service.GetDocumentsAsync(operation, take, cancellationToken)), cancellationToken);

    [HttpGet("documents/{documentId:guid}")]
    public Task<ActionResult<InventoryScanDocumentContextDto>> GetDocumentContext(
        Guid documentId,
        [FromQuery] InventoryScanOperation operation,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryScanDocumentContextDto>(async () => Ok(await _service.GetDocumentContextAsync(operation, documentId, cancellationToken)), cancellationToken);

    [HttpPost("synchronize")]
    public Task<ActionResult<InventoryScanBatchDto>> Synchronize(
        [FromBody] SynchronizeInventoryScanBatchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryScanBatchDto>(async () => Ok(await _service.SynchronizeAsync(request, CorrelationId, cancellationToken)), cancellationToken);

    [HttpGet("batches")]
    public Task<ActionResult<IReadOnlyList<InventoryScanBatchDto>>> GetRecentBatches(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryScanBatchDto>>(async () => Ok(await _service.GetRecentBatchesAsync(take, cancellationToken)), cancellationToken);

    [HttpGet("batches/{id:guid}")]
    public Task<ActionResult<InventoryScanBatchDto>> GetBatch(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryScanBatchDto>(async () => Ok(await _service.GetBatchAsync(id, cancellationToken)), cancellationToken);

    private string CorrelationId => Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action, CancellationToken cancellationToken)
    {
        try { return await action(); }
        catch (InventoryScanningAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "INV_SCAN_FORBIDDEN", exception.Message));
        }
        catch (InventoryScanningException exception)
        {
            var status = exception.Code.EndsWith("NOT_FOUND", StringComparison.Ordinal) ? 404 :
                exception.Code.Contains("CONFLICT", StringComparison.Ordinal) || exception.Code.Contains("DUPLICATE", StringComparison.Ordinal) ? 409 : 422;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_SCAN_CONCURRENCY_CONFLICT", "The resource changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Inventory scanning persistence conflict for correlation {CorrelationId}", CorrelationId);
            return Conflict(Problem(409, "INV_SCAN_PERSISTENCE_CONFLICT", "The request conflicts with a concurrently saved inventory scanning record. Refresh and retry."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem(400, "INV_SCAN_ARGUMENT_INVALID", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return UnprocessableEntity(Problem(422, "INV_SCAN_TRANSACTION_INVALID", exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Inventory mobile scanning request failed with correlation {CorrelationId}", CorrelationId);
            return StatusCode(500, Problem(500, "INV_SCAN_UNEXPECTED", "The inventory scanning request could not be completed."));
        }
        finally { _ = cancellationToken; }
    }

    private ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = code,
        Detail = detail,
        Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = CorrelationId }
    };
}
