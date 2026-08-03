using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/tracking-controls")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryTrackingControlsController : ControllerBase
{
    private readonly IInventoryTrackingControlService _service;
    private readonly ILogger<InventoryTrackingControlsController> _logger;

    public InventoryTrackingControlsController(
        IInventoryTrackingControlService service,
        ILogger<InventoryTrackingControlsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("requirements/{inventoryItemId:guid}")]
    public Task<ActionResult<InventoryTrackingRequirementsDto>> GetRequirements(
        Guid inventoryItemId,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryTrackingRequirementsDto>(async () =>
            Ok(await _service.GetRequirementsAsync(inventoryItemId, cancellationToken)));

    [HttpGet("exceptions")]
    public Task<ActionResult<IReadOnlyList<InventoryTrackingExceptionDto>>> GetExceptions(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryTrackingExceptionDto>>(async () =>
            Ok(await _service.GetExceptionsAsync(take, cancellationToken)));

    [HttpPost("exceptions")]
    public Task<ActionResult<InventoryTrackingExceptionDto>> RegisterException(
        [FromBody] RegisterInventoryTrackingExceptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryTrackingExceptionDto>(async () =>
        {
            var value = await _service.RegisterApprovedExceptionAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetExceptions), new { id = value.Id }, value);
        });

    [HttpGet("events")]
    public Task<ActionResult<IReadOnlyList<InventoryTraceabilityEventDto>>> GetEvents(
        [FromQuery] Guid? inventoryItemId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryTraceabilityEventDto>>(async () =>
            Ok(await _service.GetEventsAsync(inventoryItemId, warehouseId, take, cancellationToken)));

    private string CorrelationId =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InventoryTrackingAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "INV_TRACKING_FORBIDDEN", exception.Message));
        }
        catch (InventoryTrackingControlException exception)
        {
            var status = exception.Code.EndsWith("NOT_FOUND", StringComparison.Ordinal) ? 404 :
                exception.Code.Contains("CONFLICT", StringComparison.Ordinal) ||
                exception.Code.Contains("DUPLICATE", StringComparison.Ordinal) ||
                exception.Code.Contains("STALE", StringComparison.Ordinal) ||
                exception.Code.Contains("REUSED", StringComparison.Ordinal) ? 409 : 422;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_TRACKING_CONCURRENCY_CONFLICT",
                "The tracking record changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception,
                "Inventory tracking persistence conflict for correlation {CorrelationId}", CorrelationId);
            return Conflict(Problem(409, "INV_TRACKING_PERSISTENCE_CONFLICT",
                "The request conflicts with a concurrently saved tracking record. Refresh and retry."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Inventory tracking request failed for correlation {CorrelationId}", CorrelationId);
            return StatusCode(500, Problem(500, "INV_TRACKING_UNEXPECTED",
                "The inventory tracking request could not be completed."));
        }
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
